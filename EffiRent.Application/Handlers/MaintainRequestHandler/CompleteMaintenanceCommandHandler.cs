using EffiAP.Application.Commands.MaintainRequestCommand;
using EffiAP.Application.Services.Messaging;
using EffiAP.Application.Wrappers;
using EffiAP.Domain.Models;
using EffiAP.Domain.ViewModels.MaintainRequest;
using EffiAP.Infrastructure.IRepositories;
using EffiAP.Infrastructure.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Application.Handlers.MaintainRequestHandler
{
    public class CompleteMaintenanceCommandHandler : IRequestHandler<CompleteMaintenanceCommand, ApiResponse<CompleteMaintenanceRequestDTO>>
    {
        private readonly IRabbitMQProducerService _rabbitMQProducer;
        private readonly IUnitOfWork _unitOfWork;


        public CompleteMaintenanceCommandHandler(IRabbitMQProducerService maintenanceCommandService, IUnitOfWork unitOfWork)
        {
            _rabbitMQProducer = maintenanceCommandService;
            _unitOfWork = unitOfWork;
        }

        public async Task<ApiResponse<CompleteMaintenanceRequestDTO>> Handle(CompleteMaintenanceCommand requestComplete, CancellationToken cancellationToken)
        {
            // Kiểm tra yêu cầu
            var request = await _unitOfWork.Repository.GetOneAsync<MaintenanceRequest>(m => m.Id == requestComplete.CompleteRequestDto.RequestId);

            if (request == null)
            {
                return new ApiResponse<CompleteMaintenanceRequestDTO>("Maintenance request not found.")
                {
                    Succeeded = false,
                };
            }

            // Gán hoàn thành cho yêu cầu
            request.Status = "Completed"; // Cập nhật trạng thái
            request.TechnicianId = requestComplete.CompleteRequestDto.TechnicianId; // Gán kỹ thuật viên
            
            try
            {
                // Gọi hàm để gửi thông điệp tới completion_queue
                await _rabbitMQProducer.SendToCompletionQueueAsync(requestComplete.CompleteRequestDto.TechnicianId);

                await _unitOfWork.SaveChangesAsync(); // Lưu thay đổi


                return new ApiResponse<CompleteMaintenanceRequestDTO>(requestComplete.CompleteRequestDto, "Maintenance request completed successfully.");
            }
            catch (Exception ex)
            {
                return new ApiResponse<CompleteMaintenanceRequestDTO>($"Error completing request: {ex.Message}")
                {
                    Succeeded = false,
                };
            }
        }
    }

}
