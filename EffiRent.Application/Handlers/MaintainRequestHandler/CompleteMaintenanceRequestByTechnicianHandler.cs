using EffiAP.Application.Commands.MaintainRequestCommand;
using EffiAP.Application.Services.Messaging;
using EffiAP.Application.Wrappers;
using EffiAP.Domain.Models;
using EffiAP.Domain.ViewModels.MaintainRequest;
using EffiAP.Infrastructure.IRepositories;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Application.Handlers.MaintainRequestHandler
{
    public class CompleteMaintenanceRequestByTechnicianHandler : IRequestHandler<CompleteMaintenanceRequestByTechnicianCommand, bool>
    {
        private readonly IRabbitMQProducerService _rabbitMQProducer;
        private readonly IUnitOfWork _unitOfWork;

        public CompleteMaintenanceRequestByTechnicianHandler(IUnitOfWork unitOfWork, IRabbitMQProducerService rabbitMQProducer)
        {
            _unitOfWork = unitOfWork;
            _rabbitMQProducer = rabbitMQProducer;
        }

        public async Task<bool> Handle(CompleteMaintenanceRequestByTechnicianCommand request, CancellationToken cancellationToken)
        {
            var maintenanceRequest = await _unitOfWork.Repository.GetOneAsync<MaintenanceRequest>(r => r.Id == request.RequestId);

            if (maintenanceRequest == null || maintenanceRequest.TechnicianId != request.TechnicianId)
                throw new ArgumentException("Invalid request ID or technician ID.");

            maintenanceRequest.Status = "TechnicianCompleted";
            maintenanceRequest.TechnicianConfirmed = true;
            maintenanceRequest.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Repository.UpdateAsync<MaintenanceRequest>(maintenanceRequest);
            await _unitOfWork.SaveChangesAsync();

            try
            {
                // Gọi hàm để gửi thông điệp tới completion_queue
                await _rabbitMQProducer.SendToCompletionQueueAsync(request.TechnicianId);

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }


 


        }
    }

}
