using EffiAP.Application.Commands.MaintainRequestCommand;
using EffiAP.Application.Services.Messaging;
using EffiAP.Application.Services.Upload.Cloudinary;
using EffiAP.Application.Wrappers;
using EffiAP.Domain.Entities;
using EffiAP.Domain.Models;
using EffiAP.Domain.ViewModels.MaintainRequest;
using EffiAP.Infrastructure.IRepositories;
using EffiHR.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EffiAP.Application.Handlers.MaintainRequestHandler
{
    public class AssignTechnicianCommandHandler : IRequestHandler<AssignTechnicianCommand, ApiResponse<MaintenanceRequestCommandDTO>>
    {
        private readonly IApplicationRoleRepository _applicationRoleRepository;
        private readonly IApplicationUserRoleRepository _applicationUserRoleRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IRabbitMQProducerService  _rabbitMQProducer;
        private readonly ICloudinaryService _cloudinaryService;
        private int lastAssignedTechnicianIndex = -1;

        public AssignTechnicianCommandHandler(
                IApplicationRoleRepository applicationRoleRepository, IApplicationUserRoleRepository applicationUserRoleRepository, IRabbitMQProducerService rabbitMQProducer, IUnitOfWork unitOfWork, ICloudinaryService cloudinaryService)
        {
            _applicationRoleRepository = applicationRoleRepository;
            _applicationUserRoleRepository = applicationUserRoleRepository;

            _rabbitMQProducer = rabbitMQProducer;
            _unitOfWork = unitOfWork;
            _cloudinaryService = cloudinaryService;
        }

        public async Task<ApiResponse<MaintenanceRequestCommandDTO>> Handle(AssignTechnicianCommand request, CancellationToken cancellationToken)
        {
            using var transaction = _unitOfWork.BeginTransaction();
            var uploadedImageUrls = new List<string>();


            try
            {
                // Lấy ID của vai trò Technician
                var technicianRole = await _applicationRoleRepository.GetTechnicianRoleIdAsync();

                if (technicianRole == null)
                    throw new Exception("Technician role does not exist.");

                // Lấy danh sách kỹ thuật viên
                var technicianUserIds = await _applicationUserRoleRepository.GetTechniciansAsync(technicianRole);


                // Kiểm tra kỹ thuật viên khả dụng
                List<string> availableTechnicianIds = await GetAvailableTechniciansAsync(technicianUserIds);

                if (availableTechnicianIds.Count == 0)
                {
                    return new ApiResponse<MaintenanceRequestCommandDTO>("No available technicians; request has been queued.")
                    {
                        Succeeded = false,
                        Data = request.RequestDto
                    };
                }


                lastAssignedTechnicianIndex = (lastAssignedTechnicianIndex + 1) % availableTechnicianIds.Count;
                    var technicianToAssign = availableTechnicianIds[lastAssignedTechnicianIndex];

                    var maintenanceRequest = new MaintenanceRequest
                    {
                        CustomerId = request.RequestDto.CustomerId,
                        TechnicianId = technicianToAssign,
                        Status = "Pending",
                        PriorityLevel = request.RequestDto.PriorityLevel,
                        //CreatedAt = request.RequestDto.CreatedAt,
                        CategoryId = request.RequestDto.CategoryId,
                        RoomId = request.RequestDto.RoomId,
                    };


                await _unitOfWork.Repository.AddAsync(maintenanceRequest);
                await _unitOfWork.SaveChangesAsync();



                if (request.Image != null && request.Image.Any())
                    {
                        var maintenanceRequestImages = new List<MaintenanceRequestImage>();
                        foreach (var file in request.Image)
                        {
                            var imageUrl = _cloudinaryService.UploadPhoto(file);
                            uploadedImageUrls.Add(imageUrl);

                        maintenanceRequestImages.Add(new MaintenanceRequestImage
                            {
                                Id = Guid.NewGuid(),
                                MaintenanceRequestId = maintenanceRequest.Id,
                                ImageUrl = imageUrl,
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                        await _unitOfWork.Repository.AddRangeAsync(maintenanceRequestImages);
                    }    

                // Lưu các thay đổi và cam kết transaction
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync(transaction);

                return new ApiResponse<MaintenanceRequestCommandDTO>("Confirm request was successful")
                {
                    Succeeded = true,
                };

                
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransaction();

                //Xoá ảnh
                if (uploadedImageUrls.Count != 0)
                {
                    foreach (var imageUrl in uploadedImageUrls)
                    {
                        var publicId = _cloudinaryService.ExtractPublicId(imageUrl);
                        _cloudinaryService.DeletePhotoAsync(publicId);
                    }
                }

                return new ApiResponse<MaintenanceRequestCommandDTO>(ex.Message)
                {
                    Succeeded = false,
                };
            }
        }

        public async Task<List<string>> GetAvailableTechniciansAsync(List<string> technicianUserIds)
        {
            var availableTechnicians = new List<string>();
            var totalItems = technicianUserIds.Count;
            int pageSize = 4; // Số lượng kỹ thuật viên kiểm tra cùng một lúc

            // Khởi tạo MultiProcess
            var multiProcess = new MultiProcess();

            var pendingRequests = await _unitOfWork.Repository.Get<MaintenanceRequest>(
                req => req.Status == "Pending"
            ).ToListAsync();

            // Thực hiện các tác vụ kiểm tra
            await multiProcess.ExecuteHandler(pageSize, totalItems, async (skip, pageSize, threadIndex) =>
            {
                //var technicianId = technicianUserIds[skip];

                //var hasPendingRequests = pendingRequests.Any(req => req.TechnicianId == technicianId);

                //// Nếu không có yêu cầu bảo trì chưa hoàn thành, thêm vào danh sách kỹ thuật viên khả dụng
                //if (!hasPendingRequests)
                //{
                //    lock (availableTechnicians) // Đảm bảo an toàn khi truy cập vào danh sách
                //    {
                //        availableTechnicians.Add(technicianId);
                //    }
                //}

                var currentBatch = technicianUserIds.Skip(skip).Take(pageSize).ToList();

                foreach (var technicianId in currentBatch)
                {
                    var hasPendingRequests = pendingRequests.Any(req => req.TechnicianId == technicianId);

                    if (!hasPendingRequests)
                    {
                        lock (availableTechnicians)
                        {
                            availableTechnicians.Add(technicianId);
                        }
                    }
                }
            });

            // Sắp xếp danh sách theo thứ tự ID (chuỗi)
            availableTechnicians.Sort();

            return availableTechnicians;
        }


    }
}
