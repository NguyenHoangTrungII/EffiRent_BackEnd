using EffiAP.Application.Commands.MaintainRequestCommand;
using EffiRent.Application.Services.Maintenance;
using EffiAP.Application.Wrappers;
using EffiAP.Domain.ViewModels.MaintainRequest;
using EffiRent.Application.Services.Rabbit;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EffiRent.Application.Handlers.MaintainRequestHandler
{
    public class AssignTechnicianCommandHandler : IRequestHandler<AssignTechnicianCommand, ApiResponse<MaintenanceRequestCommandDTO>>
    {
        private readonly IMaintenanceRequestService _maintenanceRequestService;
        private readonly ILogger<AssignTechnicianCommandHandler> _logger;

        public AssignTechnicianCommandHandler(
            IMaintenanceRequestService maintenanceRequestService,
            ILogger<AssignTechnicianCommandHandler> logger)
        {
            _maintenanceRequestService = maintenanceRequestService ?? throw new ArgumentNullException(nameof(maintenanceRequestService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ApiResponse<MaintenanceRequestCommandDTO>> Handle(AssignTechnicianCommand request, CancellationToken cancellationToken)
        {
            if (request == null || request.Message?.request == null)
            {
                _logger.LogWarning("Request or MaintenanceMessage is null.");
                return new ApiResponse<MaintenanceRequestCommandDTO>("Request data is null") { Succeeded = false };
            }

            try
            {
                var result = await _maintenanceRequestService.ProcessMaintenanceRequestAsync(
                    request.Message.request, request.Message.FileBase64);

                return new ApiResponse<MaintenanceRequestCommandDTO>("Maintenance request processed successfully")
                {
                    Succeeded = true,
                    Data = result
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing AssignTechnicianCommand for CustomerId {CustomerId}", request.Message.request.CustomerId);
                return new ApiResponse<MaintenanceRequestCommandDTO>($"Error: {ex.Message}")
                {
                    Succeeded = false,
                    Errors = new List<string> { ex.ToString() }
                };
            }
        }
    }
}

//using EffiAP.Application.Commands.MaintainRequestCommand;
//using EffiAP.Application.Wrappers;
//using EffiAP.Domain.ViewModels.MaintainRequest;
//using EffiRent.Application.Services.Maintenance;
//using MediatR;
//using Microsoft.Extensions.Logging;
//using System;
//using System.Threading;
//using System.Threading.Tasks;

//namespace EffiRent.Application.Handlers.MaintainRequestHandler
//{
//    public class AssignTechnicianCommandHandler : IRequestHandler<AssignTechnicianCommand, ApiResponse<MaintenanceRequestCommandDTO>>
//    {
//        private readonly IMaintenanceRequestService _maintenanceRequestRepository; // Thêm repository
//        private readonly ILogger<AssignTechnicianCommandHandler> _logger;

//        public AssignTechnicianCommandHandler(
//            IMaintenanceRequestService maintenanceRequestRepository,
//            ILogger<AssignTechnicianCommandHandler> logger)
//        {
//            _maintenanceRequestRepository = maintenanceRequestRepository ?? throw new ArgumentNullException(nameof(maintenanceRequestRepository));
//            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
//        }

//        public async Task<ApiResponse<bool>> Handle(AssignTechnicianCommand request, CancellationToken cancellationToken)
//        {
//            if (request == null || request.Message?.request == null || request.Message.request. == null)
//            {
//                _logger.LogWarning("Request, MaintenanceMessage, or RequestId is null.");
//                return new ApiResponse<bool>("Request data is null") { Succeeded = false };
//            }

//            try
//            {
//                var maintenanceRequest = await _maintenanceRequestRepository.GetByIdAsync(request.Message.RequestId);
//                if (maintenanceRequest == null)
//                {
//                    _logger.LogWarning("Maintenance request not found for RequestId {RequestId}", request.Message.RequestId);
//                    return new ApiResponse<bool>("Maintenance request not found") { Succeeded = false };
//                }

//                // Cập nhật trạng thái và thông tin kỹ thuật viên
//                maintenanceRequest.Status = "Assigned";
//                maintenanceRequest.TechnicianId = request.Message.request.TechnicianId; // Giả sử có TechnicianId trong request
//                maintenanceRequest.UpdatedAt = DateTime.UtcNow;

//                await _maintenanceRequestRepository.UpdateAsync(maintenanceRequest);
//                await _maintenanceRequestRepository.SaveChangesAsync();

//                _logger.LogInformation("Updated maintenance request status to Assigned for RequestId {RequestId}", request.Message.RequestId);

//                return new ApiResponse<bool>("Maintenance request updated successfully")
//                {
//                    Succeeded = true,
//                    Data = true
//                };
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error updating maintenance request for RequestId {RequestId}", request.Message.RequestId);
//                return new ApiResponse<bool>($"Error: {ex.Message}")
//                {
//                    Succeeded = false,
//                    Errors = new List<string> { ex.ToString() }
//                };
//            }
//        }
//    }
//}


//using EffiAP.Application.Commands.MaintainRequestCommand;
//using EffiAP.Application.Services.Rabbit;
//using EffiAP.Application.Services.Upload.Cloudinary;
//using EffiAP.Application.Wrappers;
//using EffiAP.Domain.Entities;
//using EffiAP.Domain.Models;
//using EffiAP.Domain.ViewModels.MaintainRequest;
//using EffiAP.Infrastructure.IRepositories;
//using EffiRent.Application.Services.Rabbit;
////using MassTransit;
//using MediatR;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Logging;
//using Polly;
//using StackExchange.Redis;
//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;

//namespace EffiAP.Application.Handlers.MaintainRequestHandler
//{
//    public class AssignTechnicianCommandHandler : IRequestHandler<AssignTechnicianCommand, ApiResponse<MaintenanceRequestCommandDTO>>
//    {
//        private readonly IApplicationRoleRepository _applicationRoleRepository;
//        private readonly IApplicationUserRoleRepository _applicationUserRoleRepository;
//        private readonly IUnitOfWork _unitOfWork;
//        private readonly IRabbitMQProducerService _rabbitMQProducer;
//        private readonly ICloudinaryService _cloudinaryService;
//        private readonly ILogger<AssignTechnicianCommandHandler> _logger;
//        private static int lastAssignedTechnicianIndex = -1;
//        private static readonly object lockObject = new object();

//        public AssignTechnicianCommandHandler(
//            IApplicationRoleRepository applicationRoleRepository,
//            IApplicationUserRoleRepository applicationUserRoleRepository,
//            IUnitOfWork unitOfWork,
//            IRabbitMQProducerService rabbitMQProducer,
//            ICloudinaryService cloudinaryService,
//            ILogger<AssignTechnicianCommandHandler> logger)
//        {
//            _applicationRoleRepository = applicationRoleRepository ?? throw new ArgumentNullException(nameof(applicationRoleRepository));
//            _applicationUserRoleRepository = applicationUserRoleRepository ?? throw new ArgumentNullException(nameof(applicationUserRoleRepository));
//            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
//            _rabbitMQProducer = rabbitMQProducer ?? throw new ArgumentNullException(nameof(rabbitMQProducer));
//            _cloudinaryService = cloudinaryService ?? throw new ArgumentNullException(nameof(cloudinaryService));
//            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
//        }

//        public async Task<ApiResponse<MaintenanceRequestCommandDTO>> Handle(AssignTechnicianCommand request, CancellationToken cancellationToken)
//        {
//            // Kiểm tra đầu vào
//            if (request == null || request.RequestDto == null)
//            {
//                _logger.LogWarning("Request or RequestDto is null.");
//                return new ApiResponse<MaintenanceRequestCommandDTO>("Request data is null") { Succeeded = false };
//            }

//            // Transaction cho MaintenanceRequest
//            using var transaction = _unitOfWork.BeginTransaction();
//            MaintenanceRequest maintenanceRequest = null;
//            var uploadedImageUrls = new List<string>();

//            try
//            {
//                // Lấy ID của vai trò Technician
//                var technicianRole = await _applicationRoleRepository.GetTechnicianRoleIdAsync();
//                if (technicianRole == null)
//                {
//                    _logger.LogError("Technician role does not exist.");
//                    //throw new Exception("Technician role does not exist.");
//                    return new ApiResponse<MaintenanceRequestCommandDTO>("Technician role does not exist.") { Succeeded = false };

//                }

//                // Lấy danh sách kỹ thuật viên khả dụng
//                var technicianUserIds = await _applicationUserRoleRepository.GetTechniciansAsync(technicianRole);
//                var availableTechnicianIds = await GetAvailableTechniciansAsync(technicianUserIds);

//                if (availableTechnicianIds.Count == 0)
//                {
//                    _logger.LogWarning("No available technicians found for assignment. Queuing request.");

//                    // Gửi message đến maintenance_exchange để xếp hàng
//                    await _rabbitMQProducer.PublishAsync(
//                        message: new
//                        {
//                            CustomerId = request.RequestDto.CustomerId,
//                            PriorityLevel = request.RequestDto.PriorityLevel,
//                            CategoryId = request.RequestDto.CategoryId,
//                            RoomId = request.RequestDto.RoomId,
//                            CreatedAt = DateTime.UtcNow,
//                            Status = "Queued"
//                        },
//                        exchange: "maintenance_exchange",
//                        routingKey: "maintenance_request"
//                    );
//                    _logger.LogInformation("Published queued request to maintenance_exchange");

//                    return new ApiResponse<MaintenanceRequestCommandDTO>("No available technicians; request has been queued.")
//                    {
//                        Succeeded = false,
//                        Data = request.RequestDto
//                    };
//                }

//                // Gán kỹ thuật viên theo round-robin
//                string technicianToAssign;
//                lock (lockObject)
//                {
//                    lastAssignedTechnicianIndex = (lastAssignedTechnicianIndex + 1) % availableTechnicianIds.Count;
//                    technicianToAssign = availableTechnicianIds[lastAssignedTechnicianIndex];
//                }

//                // Tạo MaintenanceRequest
//                maintenanceRequest = new MaintenanceRequest
//                {
//                    CustomerId = request.RequestDto.CustomerId,
//                    TechnicianId = technicianToAssign,
//                    Status = "Pending",
//                    PriorityLevel = request.RequestDto.PriorityLevel,
//                    CategoryId = request.RequestDto.CategoryId,
//                    RoomId = request.RequestDto.RoomId,
//                    CreatedAt = DateTime.UtcNow
//                };

//                await _unitOfWork.Repository.AddAsync(maintenanceRequest);
//                await _unitOfWork.SaveChangesAsync();
//                await _unitOfWork.CommitTransactionAsync(transaction);
//                _logger.LogInformation("MaintenanceRequest {MaintenanceRequestId} created and committed", maintenanceRequest.Id);

//                // Gửi thông báo qua RabbitMQ
//                try
//                {
//                    await _rabbitMQProducer.PublishAsync(
//                        message: new
//                        {
//                            MaintenanceRequestId = maintenanceRequest.Id,
//                            CustomerId = maintenanceRequest.CustomerId,
//                            TechnicianId = maintenanceRequest.TechnicianId,
//                            Status = maintenanceRequest.Status,
//                            PriorityLevel = maintenanceRequest.PriorityLevel,
//                            CategoryId = maintenanceRequest.CategoryId,
//                            RoomId = maintenanceRequest.RoomId,
//                            CreatedAt = maintenanceRequest.CreatedAt
//                        },
//                        exchange: "maintenance_exchange",
//                        routingKey: "maintenance_request"
//                    );
//                    _logger.LogInformation("Published RabbitMQ message for MaintenanceRequest {MaintenanceRequestId}", maintenanceRequest.Id);
//                }
//                catch (Exception rabbitEx)
//                {
//                    _logger.LogError(rabbitEx, "Failed to publish RabbitMQ message for MaintenanceRequest {MaintenanceRequestId}", maintenanceRequest.Id);
//                    // Không làm hủy yêu cầu, chỉ ghi log
//                }

//                // Xử lý hình ảnh (ngoài transaction)
//                if (request.Image != null && request.Image.Any())
//                {
//                    var maintenanceRequestImages = new List<MaintenanceRequestImage>();
//                    foreach (var file in request.Image)
//                    {
//                        try
//                        {
//                            // Giả sử UploadPhotoAsync trả về Task<string>
//                            var imageUrl = await _cloudinaryService.UploadPhotoAsync(file);
//                            uploadedImageUrls.Add(imageUrl);
//                            maintenanceRequestImages.Add(new MaintenanceRequestImage
//                            {
//                                Id = Guid.NewGuid(),
//                                MaintenanceRequestId = maintenanceRequest.Id,
//                                ImageUrl = imageUrl,
//                                CreatedAt = DateTime.UtcNow
//                            });
//                        }
//                        catch (Exception uploadEx)
//                        {
//                            _logger.LogWarning(uploadEx, "Failed to upload image for MaintenanceRequest {MaintenanceRequestId}", maintenanceRequest.Id);
//                            // Tiếp tục với ảnh tiếp theo
//                        }
//                    }

//                    // Lưu MaintenanceRequestImage
//                    if (maintenanceRequestImages.Any())
//                    {
//                        try
//                        {
//                            await _unitOfWork.Repository.AddRangeAsync(maintenanceRequestImages);
//                            await _unitOfWork.SaveChangesAsync();
//                            _logger.LogInformation("Saved {ImageCount} images for MaintenanceRequest {MaintenanceRequestId}", maintenanceRequestImages.Count, maintenanceRequest.Id);
//                        }
//                        catch (Exception imageSaveEx)
//                        {
//                            _logger.LogError(imageSaveEx, "Failed to save images for MaintenanceRequest {MaintenanceRequestId}", maintenanceRequest.Id);
//                            // Không rollback MaintenanceRequest, chỉ ghi log
//                        }
//                    }
//                }

//                // Trả về kết quả
//                return new ApiResponse<MaintenanceRequestCommandDTO>("Confirm request was successful")
//                {
//                    Succeeded = true,
//                    Data = new MaintenanceRequestCommandDTO
//                    {
//                        requestId = maintenanceRequest.Id,
//                        CustomerId = maintenanceRequest.CustomerId,
//                        TechnicianId = maintenanceRequest.TechnicianId,
//                        Status = maintenanceRequest.Status,
//                        PriorityLevel = maintenanceRequest.PriorityLevel,
//                        CategoryId = maintenanceRequest.CategoryId,
//                        RoomId = maintenanceRequest.RoomId,
//                        CreatedAt = maintenanceRequest.CreatedAt
//                    }
//                };
//            }
//            catch (Exception ex)
//            {
//                await _unitOfWork.RollbackTransactionAsync(transaction);
//                _logger.LogError(ex, "Failed to process AssignTechnicianCommand for CustomerId {CustomerId}", request.RequestDto.CustomerId);

//                // Xóa ảnh nếu đã upload
//                foreach (var imageUrl in uploadedImageUrls)
//                {
//                    try
//                    {
//                        var publicId = _cloudinaryService.ExtractPublicId(imageUrl);
//                        await _cloudinaryService.DeletePhotoAsync(publicId);
//                        _logger.LogInformation("Deleted image {ImageUrl} from Cloudinary", imageUrl);
//                    }
//                    catch (Exception deleteEx)
//                    {
//                        _logger.LogWarning(deleteEx, "Failed to delete image {ImageUrl} from Cloudinary", imageUrl);
//                    }
//                }

//                return new ApiResponse<MaintenanceRequestCommandDTO>($"Error assigning technician: {ex.Message}")
//                {
//                    Succeeded = false,
//                    Errors = new List<string> { ex.ToString() }
//                };
//            }
//        }

//        public async Task<List<string>> GetAvailableTechniciansAsync(List<string> technicianUserIds)
//        {
//            var availableTechnicians = new ConcurrentBag<string>();

//            // Lấy danh sách TechnicianId đang có yêu cầu Pending
//            //var pendingTechnicians = await _unitOfWork.Repository.Get<MaintenanceRequest>()
//            //    .Where(req => req.Status == "Pending" && req.TechnicianId != null)
//            //    .Select(req => req.TechnicianId)
//            //    .Distinct()
//            //    .ToListAsync();

//            var pendingTechnicians = _unitOfWork.Repository.Get<MaintenanceRequest>()
//                .Where(req => req.Status == "Pending" && req.TechnicianId != null)
//                .Select(req => req.TechnicianId)
//                .Distinct()
//                .ToList();

//            var pendingRequestSet = new HashSet<string>(pendingTechnicians);

//            // Kiểm tra từng kỹ thuật viên
//            Parallel.ForEach(technicianUserIds, technicianId =>
//            {
//                if (!pendingRequestSet.Contains(technicianId))
//                {
//                    availableTechnicians.Add(technicianId);
//                }
//            });

//            var result = availableTechnicians.ToList();
//            result.Sort(); // Sắp xếp để đảm bảo thứ tự nhất quán
//            return result;
//        }


//    }
//}

//using EffiAP.Application.Commands.MaintainRequestCommand;
//using EffiAP.Application.Services.Messaging;
//using EffiAP.Application.Services.Upload.Cloudinary;
//using EffiAP.Application.Wrappers;
//using EffiAP.Domain.Entities;
//using EffiAP.Domain.Models;
//using EffiAP.Domain.ViewModels.MaintainRequest;
//using EffiAP.Infrastructure.IRepositories;
//using EffiHR.Application.Services;
//using MediatR;
//using Microsoft.EntityFrameworkCore;
//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;
//using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
//using static System.Runtime.InteropServices.JavaScript.JSType;

//namespace EffiAP.Application.Handlers.MaintainRequestHandler
//{
//    public class AssignTechnicianCommandHandler : IRequestHandler<AssignTechnicianCommand, ApiResponse<MaintenanceRequestCommandDTO>>
//    {
//        private readonly IApplicationRoleRepository _applicationRoleRepository;
//        private readonly IApplicationUserRoleRepository _applicationUserRoleRepository;
//        private readonly IUnitOfWork _unitOfWork;
//        private readonly IRabbitMQProducerService  _rabbitMQProducer;
//        private readonly ICloudinaryService _cloudinaryService;
//        private int lastAssignedTechnicianIndex = -1;

//        public AssignTechnicianCommandHandler(
//                IApplicationRoleRepository applicationRoleRepository, IApplicationUserRoleRepository applicationUserRoleRepository, IRabbitMQProducerService rabbitMQProducer, IUnitOfWork unitOfWork, ICloudinaryService cloudinaryService)
//        {
//            _applicationRoleRepository = applicationRoleRepository;
//            _applicationUserRoleRepository = applicationUserRoleRepository;

//            _rabbitMQProducer = rabbitMQProducer;
//            _unitOfWork = unitOfWork;
//            _cloudinaryService = cloudinaryService;
//        }

//        public async Task<ApiResponse<MaintenanceRequestCommandDTO>> Handle(AssignTechnicianCommand request, CancellationToken cancellationToken)
//        {
//            using var transaction = _unitOfWork.BeginTransaction();
//            var uploadedImageUrls = new List<string>();


//            try
//            {
//                // Lấy ID của vai trò Technician
//                var technicianRole = await _applicationRoleRepository.GetTechnicianRoleIdAsync();

//                if (technicianRole == null)
//                    throw new Exception("Technician role does not exist.");

//                // Lấy danh sách kỹ thuật viên
//                var technicianUserIds = await _applicationUserRoleRepository.GetTechniciansAsync(technicianRole);


//                // Kiểm tra kỹ thuật viên khả dụng
//                List<string> availableTechnicianIds = await GetAvailableTechniciansAsync(technicianUserIds);

//                if (availableTechnicianIds.Count == 0)
//                {
//                    return new ApiResponse<MaintenanceRequestCommandDTO>("No available technicians; request has been queued.")
//                    {
//                        Succeeded = false,
//                        Data = request.RequestDto
//                    };
//                }


//                lastAssignedTechnicianIndex = (lastAssignedTechnicianIndex + 1) % availableTechnicianIds.Count;
//                    var technicianToAssign = availableTechnicianIds[lastAssignedTechnicianIndex];

//                    var maintenanceRequest = new MaintenanceRequest
//                    {
//                        CustomerId = request.RequestDto.CustomerId,
//                        TechnicianId = technicianToAssign,
//                        Status = "Pending",
//                        PriorityLevel = request.RequestDto.PriorityLevel,
//                        //CreatedAt = request.RequestDto.CreatedAt,
//                        CategoryId = request.RequestDto.CategoryId,
//                        RoomId = request.RequestDto.RoomId,
//                    };


//                await _unitOfWork.Repository.AddAsync(maintenanceRequest);
//                await _unitOfWork.SaveChangesAsync();



//                if (request.Image != null && request.Image.Any())
//                    {
//                        var maintenanceRequestImages = new List<MaintenanceRequestImage>();
//                        foreach (var file in request.Image)
//                        {
//                            var imageUrl = _cloudinaryService.UploadPhoto(file);
//                            uploadedImageUrls.Add(imageUrl);

//                        maintenanceRequestImages.Add(new MaintenanceRequestImage
//                            {
//                                Id = Guid.NewGuid(),
//                                MaintenanceRequestId = maintenanceRequest.Id,
//                                ImageUrl = imageUrl,
//                                CreatedAt = DateTime.UtcNow
//                            });
//                        }
//                        await _unitOfWork.Repository.AddRangeAsync(maintenanceRequestImages);
//                    }    

//                // Lưu các thay đổi và cam kết transaction
//                await _unitOfWork.SaveChangesAsync();
//                await _unitOfWork.CommitTransactionAsync(transaction);

//                return new ApiResponse<MaintenanceRequestCommandDTO>("Confirm request was successful")
//                {
//                    Succeeded = true,
//                };


//            }
//            catch (Exception ex)
//            {
//                await _unitOfWork.RollbackTransaction();

//                //Xoá ảnh
//                if (uploadedImageUrls.Count != 0)
//                {
//                    foreach (var imageUrl in uploadedImageUrls)
//                    {
//                        var publicId = _cloudinaryService.ExtractPublicId(imageUrl);
//                        _cloudinaryService.DeletePhotoAsync(publicId);
//                    }
//                }

//                return new ApiResponse<MaintenanceRequestCommandDTO>(ex.Message)
//                {
//                    Succeeded = false,
//                };
//            }
//        }

//        public async Task<List<string>> GetAvailableTechniciansAsync(List<string> technicianUserIds)
//        {
//            //var availableTechnicians = new List<string>();
//            var availableTechnicians = new ConcurrentBag<string>();
//            var totalItems = technicianUserIds.Count;
//            int pageSize = 4; // Số lượng kỹ thuật viên kiểm tra cùng một lúc

//            // Khởi tạo MultiProcess
//            var multiProcess = new MultiProcess();

//            var pendingRequests = await _unitOfWork.Repository.Get<MaintenanceRequest>(
//                req => req.Status == "Pending"
//            ).ToListAsync();

//            var pendingRequestSet = new HashSet<string>(pendingRequests.Select(req => req.TechnicianId));


//            // Thực hiện các tác vụ kiểm tra
//            await multiProcess.ExecuteHandler(pageSize, totalItems, async (skip, pageSize, threadIndex) =>
//            {

//                var currentBatch = technicianUserIds.Skip(skip).Take(pageSize).ToList();

//                foreach (var technicianId in currentBatch)
//                {
//                    //var hasPendingRequests = pendingRequests.Any(req => req.TechnicianId == technicianId);

//                    //if (!hasPendingRequests)
//                    //{

//                    //    availableTechnicians.Add(technicianId);
//                    //}
//                    if (!pendingRequestSet.Contains(technicianId))
//            {
//                availableTechnicians.Add(technicianId);
//            }
//                }
//            });

//            // Sắp xếp danh sách theo thứ tự ID (chuỗi)
//            //availableTechnicians.Sort();
//            var result = availableTechnicians.ToList();
//            result.Sort();
//            return result;

//            //return availableTechnicians;
//        }


//    }
//}


//using EffiAP.Application.Commands.MaintainRequestCommand;
//using EffiAP.Application.Services.Upload.Cloudinary;
////using EffiAP.Application.Wrappers;
//using EffiAP.Domain.Entities;
//using EffiAP.Domain.Models;
//using EffiAP.Domain.ViewModels.MaintainRequest;
//using EffiAP.Infrastructure.IRepositories;
//using MassTransit; // Sử dụng MassTransit thay vì RabbitMQProducer
//using MediatR;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Logging;
//using Polly; // Thêm Polly cho retry
//using StackExchange.Redis; // Thêm Redis
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;

//namespace EffiAP.Application.Handlers.MaintainRequestHandler
//{
//    public class AssignTechnicianCommandHandler : IRequestHandler<AssignTechnicianCommand, Response<MaintenanceRequestCommandDTO>>
//    {
//        private readonly IApplicationRoleRepository _applicationRoleRepository;
//        private readonly IApplicationUserRoleRepository _applicationUserRoleRepository;
//        private readonly IUnitOfWork _unitOfWork;
//        private readonly IPublishEndpoint _publishEndpoint; // MassTransit
//        private readonly ICloudinaryService _cloudinaryService;
//        private readonly IConnectionMultiplexer _redis; // Redis
//        private readonly ILogger<AssignTechnicianCommandHandler> _logger;
//        private readonly Policy _retryPolicy; // Polly retry policy

//        public AssignTechnicianCommandHandler(
//            IApplicationRoleRepository applicationRoleRepository,
//            IApplicationUserRoleRepository applicationUserRoleRepository,
//            IUnitOfWork unitOfWork,
//            IPublishEndpoint publishEndpoint,
//            ICloudinaryService cloudinaryService,
//            IConnectionMultiplexer redis,
//            ILogger<AssignTechnicianCommandHandler> logger)
//        {
//            _applicationRoleRepository = applicationRoleRepository ?? throw new ArgumentNullException(nameof(applicationRoleRepository));
//            _applicationUserRoleRepository = applicationUserRoleRepository ?? throw new ArgumentNullException(nameof(applicationUserRoleRepository));
//            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
//            _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
//            _cloudinaryService = cloudinaryService ?? throw new ArgumentNullException(nameof(cloudinaryService));
//            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
//            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

//            // Định nghĩa retry policy với Polly
//            _retryPolicy = Policy
//                .Handle<Exception>()
//                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
//                    (exception, timeSpan, retryCount, context) =>
//                        _logger.LogWarning(exception, "Retry {RetryCount} after {TimeSpan} seconds due to error.", retryCount, timeSpan.TotalSeconds));
//        }

//        public async Task<ApiResponse<MaintenanceRequestCommandDTO>> Handle(AssignTechnicianCommand request, CancellationToken cancellationToken)
//        {
//            if (request?.RequestDto == null)
//            {
//                _logger.LogWarning("Request or RequestDto is null.");
//                return Response<MaintenanceRequestCommandDTO>.Failure("Request data is null");
//            }

//            using var transaction = _unitOfWork.BeginTransaction();
//            var uploadedImageUrls = new List<string>();

//            try
//            {
//                // Lấy ID vai trò Technician
//                var technicianRole = await _applicationRoleRepository.GetTechnicianRoleIdAsync();
//                if (technicianRole == null)
//                {
//                    _logger.LogError("Technician role does not exist.");
//                    return Response<MaintenanceRequestCommandDTO>.Failure("Technician role does not exist.");
//                }

//                // Lấy danh sách kỹ thuật viên khả dụng
//                var technicianUserIds = await _applicationUserRoleRepository.GetTechniciansAsync(technicianRole);
//                var availableTechnicianIds = await GetAvailableTechniciansAsync(technicianUserIds);

//                if (!availableTechnicianIds.Any())
//                {
//                    _logger.LogWarning("No available technicians found. Queuing request.");
//                    await _publishEndpoint.Publish(new
//                    {
//                        request.RequestDto.CustomerId,
//                        request.RequestDto.PriorityLevel,
//                        request.RequestDto.CategoryId,
//                        request.RequestDto.RoomId,
//                        CreatedAt = DateTime.UtcNow,
//                        Status = "Queued"
//                    }, cancellationToken);

//                    return Response<MaintenanceRequestCommandDTO>.Failure("No available technicians; request has been queued.", request.RequestDto);
//                }

//                // Gán kỹ thuật viên bằng Redis
//                var technicianToAssign = await AssignTechnicianRoundRobinAsync(availableTechnicianIds);

//                // Upload ảnh lên Cloudinary và chuẩn bị MaintenanceRequestImage
//                var maintenanceRequestImages = new List<MaintenanceRequestImage>();
//                if (request.Image?.Any() == true)
//                {
//                    foreach (var file in request.Image)
//                    {
//                        var imageUrl = await _retryPolicy.ExecuteAsync(() => _cloudinaryService.UploadPhotoAsync(file));
//                        uploadedImageUrls.Add(imageUrl);
//                        maintenanceRequestImages.Add(new MaintenanceRequestImage
//                        {
//                            Id = Guid.NewGuid(),
//                            MaintenanceRequestId = Guid.Empty, // Sẽ cập nhật sau
//                            ImageUrl = imageUrl,
//                            CreatedAt = DateTime.UtcNow
//                        });
//                    }
//                }

//                // Tạo MaintenanceRequest
//                var maintenanceRequest = new MaintenanceRequest
//                {
//                    CustomerId = request.RequestDto.CustomerId,
//                    TechnicianId = technicianToAssign,
//                    Status = "Pending",
//                    PriorityLevel = request.RequestDto.PriorityLevel,
//                    CategoryId = request.RequestDto.CategoryId,
//                    RoomId = request.RequestDto.RoomId,
//                    CreatedAt = DateTime.UtcNow
//                };

//                await _unitOfWork.Repository.AddAsync(maintenanceRequest);

//                // Cập nhật MaintenanceRequestId cho ảnh và lưu
//                foreach (var image in maintenanceRequestImages)
//                {
//                    image.MaintenanceRequestId = maintenanceRequest.Id;
//                }
//                if (maintenanceRequestImages.Any())
//                {
//                    await _unitOfWork.Repository.AddRangeAsync(maintenanceRequestImages);
//                }

//                await _unitOfWork.SaveChangesAsync();
//                await _unitOfWork.CommitTransactionAsync(transaction);
//                _logger.LogInformation("MaintenanceRequest {MaintenanceRequestId} created with {ImageCount} images", maintenanceRequest.Id, maintenanceRequestImages.Count);

//                // Publish thông báo qua MassTransit
//                await _retryPolicy.ExecuteAsync(() => _publishEndpoint.Publish(new
//                {
//                    MaintenanceRequestId = maintenanceRequest.Id,
//                    maintenanceRequest.CustomerId,
//                    maintenanceRequest.TechnicianId,
//                    maintenanceRequest.Status,
//                    maintenanceRequest.PriorityLevel,
//                    maintenanceRequest.CategoryId,
//                    maintenanceRequest.RoomId,
//                    maintenanceRequest.CreatedAt
//                }, cancellationToken));

//                return Response<MaintenanceRequestCommandDTO>.Success("Request assigned successfully", new MaintenanceRequestCommandDTO
//                {
//                    requestId = maintenanceRequest.Id,
//                    CustomerId = maintenanceRequest.CustomerId,
//                    TechnicianId = maintenanceRequest.TechnicianId,
//                    Status = maintenanceRequest.Status,
//                    PriorityLevel = maintenanceRequest.PriorityLevel,
//                    CategoryId = maintenanceRequest.CategoryId,
//                    RoomId = maintenanceRequest.RoomId,
//                    CreatedAt = maintenanceRequest.CreatedAt
//                });
//            }
//            catch (Exception ex)
//            {
//                await _unitOfWork.RollbackTransactionAsync(transaction);
//                _logger.LogError(ex, "Failed to process AssignTechnicianCommand for CustomerId {CustomerId}", request.RequestDto.CustomerId);

//                // Xóa ảnh đã upload
//                foreach (var imageUrl in uploadedImageUrls)
//                {
//                    await _retryPolicy.ExecuteAsync(() => _cloudinaryService.DeletePhotoAsync(_cloudinaryService.ExtractPublicId(imageUrl)));
//                }

//                return Response<MaintenanceRequestCommandDTO>.Failure($"Error: {ex.Message}", errors: new[] { ex.ToString() });
//            }
//        }

//        private async Task<List<string>> GetAvailableTechniciansAsync(List<string> technicianUserIds)
//        {
//            var pendingTechnicians = await _unitOfWork.Repository.Get<MaintenanceRequest>()
//                .AsNoTracking()
//                .Where(req => req.Status == "Pending" && req.TechnicianId != null)
//                .Select(req => req.TechnicianId)
//                .Distinct()
//                .ToListAsync();

//            var availableTechnicians = technicianUserIds.Except(pendingTechnicians).ToList();
//            availableTechnicians.Sort();
//            return availableTechnicians;
//        }

//        private async Task<string> AssignTechnicianRoundRobinAsync(List<string> technicianIds)
//        {
//            var db = _redis.GetDatabase();
//            var key = "technician:round-robin:index";
//            var index = await db.StringIncrementAsync(key) % technicianIds.Count;
//            return technicianIds[(int)index];
//        }
//    }

//    // Helper class để đơn giản hóa ApiResponse
//    //public static class ApiResponse<T>
//    //{
//    //    public static ApiResponse<T> Success(string message, T data = default) => new() { Succeeded = true, Message = message, Data = data };
//    //    public static ApiResponse<T> Failure(string message, T data = default, IEnumerable<string> errors = null) => new()
//    //    {
//    //        Succeeded = false,
//    //        Message = message,
//    //        Data = data,
//    //        Errors = errors?.ToList()
//    //    };
//    //}

//    // Class để biểu diễn phản hồi API
//    public class Response<T>
//    {
//        public bool Succeeded { get; set; }
//        public string Message { get; set; }
//        public T Data { get; set; }
//        public List<string> Errors { get; set; }

//        // Constructor mặc định
//        public Response() { }

//        // Factory methods để tạo instance
//        public static Response<T> Success(string message, T data = default) => new()
//        {
//            Succeeded = true,
//            Message = message,
//            Data = data,
//            Errors = null
//        };

//        public static Response<T> Failure(string message, T data = default, IEnumerable<string> errors = null) => new()
//        {
//            Succeeded = false,
//            Message = message,
//            Data = data,
//            Errors = errors?.ToList()
//        };
//    }

//}

