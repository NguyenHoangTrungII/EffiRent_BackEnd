using EffiRent.Domain.Entities;
//using EffiAP.Domain.Models;
using EffiAP.Domain.ViewModels.MaintainRequest;
using EffiAP.Infrastructure.IRepositories;
using EffiRent.Application.Services.Rabbit;
using EffiRent.Application.Services.Technician;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace EffiRent.Application.Services.Maintenance
{
    public class MaintenanceRequestService : IMaintenanceRequestService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITechnicianAssignmentService _technicianAssignmentService;
        private readonly ILogger<MaintenanceRequestService> _logger;

        public MaintenanceRequestService(
            IUnitOfWork unitOfWork,
            ITechnicianAssignmentService technicianAssignmentService,
            ILogger<MaintenanceRequestService> logger)
        {
            _unitOfWork = unitOfWork;
            _technicianAssignmentService = technicianAssignmentService;
            _logger = logger;
        }

        public async Task<MaintenanceRequestCommandDTO> ProcessMaintenanceRequestAsync(MaintenanceRequestCommandDTO request, List<string> fileUrls)
        {
            using var transaction = _unitOfWork.BeginTransaction();

            try
            {

                // Lấy MaintenanceRequest hiện có dựa trên requestId
                var maintenanceRequest = await _unitOfWork.Repository.GetByIdAsync<MaintenanceRequest>(request.requestId);
                if (maintenanceRequest == null)
                {
                    _logger.LogWarning("Maintenance request not found for RequestId {RequestId}", request.requestId);
                    throw new Exception($"Maintenance request with ID {request.requestId} not found.");
                }

                // Gán kỹ thuật viên
                var technicianId = await _technicianAssignmentService.AssignTechnicianAsync(request);
                if (technicianId == null)
                {
                    maintenanceRequest.Status = "Queued";
                    _logger.LogInformation("No available technicians, setting request to Queued for RequestId {RequestId}", request.requestId);
                }
                else
                {
                    maintenanceRequest.TechnicianId = technicianId;
                    maintenanceRequest.Status = "Assigned";
                    _logger.LogInformation("Assigned technician {TechnicianId} to RequestId {RequestId}", technicianId, request.requestId);
                }

                // Cập nhật các thông tin khác nếu cần
                maintenanceRequest.UpdatedAt = DateTime.UtcNow;

                // Lưu thay đổi
                await _unitOfWork.Repository.UpdateAsync(maintenanceRequest);
                await _unitOfWork.SaveChangesAsync();

                await _unitOfWork.CommitAsync(transaction);

                // Trả về DTO với thông tin đã cập nhật
                var result = new MaintenanceRequestCommandDTO
                {
                    requestId = maintenanceRequest.Id,
                    CustomerId = maintenanceRequest.CustomerId,
                    TechnicianId = maintenanceRequest.TechnicianId,
                    Status = maintenanceRequest.Status,
                    PriorityLevel = maintenanceRequest.PriorityLevel,
                    CategoryId = maintenanceRequest.CategoryId,
                    RoomId = maintenanceRequest.RoomId,
                    CreatedAt = maintenanceRequest.CreatedAt,
                    //UpdatedAt = maintenanceRequest.UpdatedAt
                };

                return result;
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync(transaction);
                _logger.LogError(ex, "Error updating maintenance request for RequestId {RequestId}", request.requestId);
                throw;
            }
        }
    }
}

//using EffiAP.Domain.Entities;
//using EffiAP.Domain.Models;
//using EffiAP.Domain.ViewModels.MaintainRequest;
//using EffiAP.Infrastructure.IRepositories;
//using EffiRent.Application.Services.Rabbit;
//using EffiRent.Application.Services.Technician;
//using Microsoft.Extensions.Logging;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace EffiRent.Application.Services.Maintenance
//{
//    public class MaintenanceRequestService : IMaintenanceRequestService
//    {
//        private readonly IUnitOfWork _unitOfWork;
//        private readonly ITechnicianAssignmentService _technicianAssignmentService;
//        private readonly IRabbitMQProducerService _rabbitMQProducer;
//        private readonly ILogger<MaintenanceRequestService> _logger;

//        public MaintenanceRequestService(
//            IUnitOfWork unitOfWork,
//            ITechnicianAssignmentService technicianAssignmentService,
//            IRabbitMQProducerService rabbitMQProducer,
//            ILogger<MaintenanceRequestService> logger)
//        {
//            _unitOfWork = unitOfWork;
//            _technicianAssignmentService = technicianAssignmentService;
//            _rabbitMQProducer = rabbitMQProducer;
//            _logger = logger;
//        }

//        public async Task<MaintenanceRequestCommandDTO> ProcessMaintenanceRequestAsync(MaintenanceRequestCommandDTO request, List<string> fileUrls)
//        {
//            using var transaction = _unitOfWork.BeginTransaction();

//            try
//            {
//                var maintenanceRequest = new MaintenanceRequest
//                {
//                    CustomerId = request.CustomerId,
//                    Status = "Pending",
//                    PriorityLevel = request.PriorityLevel,
//                    CategoryId = request.CategoryId,
//                    RoomId = request.RoomId,
//                    CreatedAt = DateTime.UtcNow
//                };

//                var technicianId = await _technicianAssignmentService.AssignTechnicianAsync(request);
//                if (technicianId == null)
//                {
//                    maintenanceRequest.Status = "Queued";
//                    _logger.LogInformation("No available technicians, queuing request for CustomerId {CustomerId}", request.CustomerId);
//                }
//                else
//                {
//                    maintenanceRequest.TechnicianId = technicianId;
//                    maintenanceRequest.Status = "Assigned";
//                }

//                await _unitOfWork.Repository.AddAsync(maintenanceRequest);
//                await _unitOfWork.SaveChangesAsync();

//                if (fileUrls != null && fileUrls.Any())
//                {
//                    var images = fileUrls.Select(url => new MaintenanceRequestImage
//                    {
//                        Id = Guid.NewGuid(),
//                        MaintenanceRequestId = maintenanceRequest.Id,
//                        ImageUrl = url,
//                        CreatedAt = DateTime.UtcNow
//                    }).ToList();

//                    await _unitOfWork.Repository.AddRangeAsync(images);
//                    await _unitOfWork.SaveChangesAsync();
//                }

//                await _unitOfWork.CommitTransactionAsync(transaction);

//                var result = new MaintenanceRequestCommandDTO
//                {
//                    requestId = maintenanceRequest.Id,
//                    CustomerId = maintenanceRequest.CustomerId,
//                    TechnicianId = maintenanceRequest.TechnicianId,
//                    Status = maintenanceRequest.Status,
//                    PriorityLevel = maintenanceRequest.PriorityLevel,
//                    CategoryId = maintenanceRequest.CategoryId,
//                    RoomId = maintenanceRequest.RoomId,
//                    CreatedAt = maintenanceRequest.CreatedAt
//                };

//                if (technicianId != null)
//                {
//                    var completionMessage = System.Text.Json.JsonSerializer.Serialize(new
//                    {
//                        EventType = "TechnicianAssigned",
//                        RequestId = maintenanceRequest.Id
//                    });
//                    await _rabbitMQProducer.PublishAsync(completionMessage, "completion_exchange", "completion_request");
//                    _logger.LogInformation("Sent completion event for request ID {RequestId}", maintenanceRequest.Id);
//                }

//                return result;
//            }
//            catch (Exception ex)
//            {
//                await _unitOfWork.RollbackTransactionAsync(transaction);
//                _logger.LogError(ex, "Error processing maintenance request for CustomerId {CustomerId}", request.CustomerId);
//                throw;
//            }
//        }
//    }
//}
