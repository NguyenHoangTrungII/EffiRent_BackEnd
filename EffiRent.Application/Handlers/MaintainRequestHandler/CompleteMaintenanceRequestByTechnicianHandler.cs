using MediatR;
using Microsoft.Extensions.Logging;
using EffiRent.Domain.Entities;
//using EffiAP.Infrastructure.UnitOfWork;
//using EffiAP.Infrastructure.RabbitMQ;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EffiAP.Application.Services.Redis;
//using EffiAP.Domain.Models;
using EffiAP.Domain.ViewModels.MaintainRequest;
using EffiAP.Infrastructure.IRepositories;
using EffiRent.Application.Services.Technician;
using EffiRent.Application.Services.Rabbit;
using Microsoft.EntityFrameworkCore;

namespace EffiAP.Application.Commands.MaintainRequestCommand
{
    public class CompleteMaintenanceRequestByTechnicianCommandHandler : IRequestHandler<CompleteMaintenanceRequestByTechnicianCommand, bool>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITechnicianAssignmentService _technicianAssignmentService;
        private readonly IRabbitMQProducerService _producerService;
        private readonly ILogger<CompleteMaintenanceRequestByTechnicianCommandHandler> _logger;
        private readonly IRedisService _redisService; // Tùy chọn, nếu dùng Redis

        public CompleteMaintenanceRequestByTechnicianCommandHandler(
            IUnitOfWork unitOfWork,
            ITechnicianAssignmentService technicianAssignmentService,
            IRabbitMQProducerService producerService,
            ILogger<CompleteMaintenanceRequestByTechnicianCommandHandler> logger,
            IRedisService redisService = null) // Redis là tùy chọn
        {
            _unitOfWork = unitOfWork;
            _technicianAssignmentService = technicianAssignmentService;
            _producerService = producerService;
            _logger = logger;
            _redisService = redisService;
        }

        public async Task<bool> Handle(CompleteMaintenanceRequestByTechnicianCommand request, CancellationToken cancellationToken)
        {
            try
            {
                // Bước 1: Tìm yêu cầu sửa chữa trong database
                var maintenanceRequest = await _unitOfWork.Repository.Get<MaintenanceRequest>()
                    .FirstOrDefaultAsync(r => r.Id == request.RequestId, cancellationToken);


                if (maintenanceRequest == null)
                {
                    _logger.LogWarning("Maintenance request with ID {RequestId} not found", request.RequestId);
                    return false;
                }

                // Bước 2: Kiểm tra kỹ thuật viên có được gán cho yêu cầu này không
                if (maintenanceRequest.TechnicianId != request.TechnicianId)
                {
                    _logger.LogWarning("Technician {TechnicianId} is not assigned to request {RequestId}", request.TechnicianId, request.RequestId);
                    return false;
                }

                // Bước 3: Kiểm tra trạng thái yêu cầu (không cho phép hoàn thành nếu đã hoàn thành)
                if (maintenanceRequest.Status == "Completed")
                {
                    _logger.LogWarning("Maintenance request {RequestId} is already completed", request.RequestId);
                    return false;
                }

                // Bước 4: Cập nhật trạng thái yêu cầu thành Completed
                maintenanceRequest.Status = "Completed";
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Maintenance request {RequestId} marked as Completed", request.RequestId);

                // Bước 5: Đánh dấu kỹ thuật viên là rảnh
                await _technicianAssignmentService.MarkTechnicianAvailable(request.TechnicianId);
                _logger.LogInformation("Technician {TechnicianId} marked as available after completing request {RequestId}",
                    request.TechnicianId, request.RequestId);

                // Bước 6: Gửi CompletionEvent vào completion_exchange
                var completionEvent = new CompletionEvent
                {
                    RequestId = maintenanceRequest.Id.ToString(),
                    TechnicianId = request.TechnicianId,
                    //CompletionTime = DateTime.UtcNow
                };
                var completionMessage = JsonSerializer.Serialize(completionEvent);
                await _producerService.PublishAsync(completionMessage, "completion_exchange", "completion_request");
                _logger.LogInformation("Published CompletionEvent for RequestId {RequestId} and TechnicianId {TechnicianId}",
                    request.RequestId, request.TechnicianId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to complete maintenance request {RequestId} by technician {TechnicianId}",
                    request.RequestId, request.TechnicianId);
                return false;
            }
        }
    }
}

//using EffiAP.Application.Commands.MaintainRequestCommand;
//using EffiAP.Application.Services.Messaging;
//using EffiAP.Application.Wrappers;
//using EffiAP.Domain.Models;
//using EffiAP.Domain.ViewModels.MaintainRequest;
//using EffiAP.Infrastructure.IRepositories;
//using MediatR;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace EffiAP.Application.Handlers.MaintainRequestHandler
//{
//    public class CompleteMaintenanceRequestByTechnicianHandler : IRequestHandler<CompleteMaintenanceRequestByTechnicianCommand, bool>
//    {
//        private readonly IRabbitMQProducerService _rabbitMQProducer;
//        private readonly IUnitOfWork _unitOfWork;

//        public CompleteMaintenanceRequestByTechnicianHandler(IUnitOfWork unitOfWork, IRabbitMQProducerService rabbitMQProducer)
//        {
//            _unitOfWork = unitOfWork;
//            _rabbitMQProducer = rabbitMQProducer;
//        }

//        public async Task<bool> Handle(CompleteMaintenanceRequestByTechnicianCommand request, CancellationToken cancellationToken)
//        {
//            var maintenanceRequest = await _unitOfWork.Repository.GetOneAsync<MaintenanceRequest>(r => r.Id == request.RequestId);

//            if (maintenanceRequest == null || maintenanceRequest.TechnicianId != request.TechnicianId)
//                throw new ArgumentException("Invalid request ID or technician ID.");

//            maintenanceRequest.Status = "TechnicianCompleted";
//            maintenanceRequest.TechnicianConfirmed = true;
//            maintenanceRequest.UpdatedAt = DateTime.UtcNow;

//            _unitOfWork.Repository.UpdateAsync<MaintenanceRequest>(maintenanceRequest);
//            await _unitOfWork.SaveChangesAsync();

//            try
//            {
//                // Gọi hàm để gửi thông điệp tới completion_queue
//                await _rabbitMQProducer.SendToCompletionQueueAsync(request.TechnicianId);

//                return true;
//            }
//            catch (Exception ex)
//            {
//                return false;
//            }





//        }
//    }

//}
