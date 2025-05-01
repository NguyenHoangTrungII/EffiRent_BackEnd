using EffiAP.Application.Commands.MaintainRequestCommand;
using EffiAP.Application.Services.Messaging;
using EffiAP.Application.Wrappers;
using EffiRent.Domain.Entities;
using EffiAP.Domain.ViewModels.MaintainRequest;
using EffiAP.Infrastructure.IRepositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Application.Handlers.MaintainRequestHandler
{
    public class ConfirmMaintenanceCompletionByCustomerCommandHandler : IRequestHandler<ConfirmMaintenanceCompletionByCustomerCommand, ApiResponse<ConfirmMaintenanceCompletionByCustomerDTO>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public ConfirmMaintenanceCompletionByCustomerCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ApiResponse<ConfirmMaintenanceCompletionByCustomerDTO>> Handle(ConfirmMaintenanceCompletionByCustomerCommand command, CancellationToken cancellationToken)
        {
            // Bắt đầu transaction
            using var transaction = _unitOfWork.BeginTransaction();
            try
            {
                // Lấy yêu cầu bảo trì
                var maintenanceRequest = await _unitOfWork.Repository.Get<MaintenanceRequest>(mr => mr.Id == command.MaintenanceRequestId)
                                .Include(mr => mr.Feedbacks)
                                .FirstOrDefaultAsync();

                if (maintenanceRequest == null)
                {
                    return new ApiResponse<ConfirmMaintenanceCompletionByCustomerDTO>("Maintenance request not found.")
                    {
                        Succeeded = false,
                    };
                }

                // Kiểm tra xem yêu cầu đã được nhân viên xác nhận chưa
                if (!maintenanceRequest.TechnicianConfirmed)
                {
                    return new ApiResponse<ConfirmMaintenanceCompletionByCustomerDTO>("The maintenance request has not been confirmed by the technician.")
                    {
                        Succeeded = false,
                    };
                }

                // Cập nhật trạng thái yêu cầu bảo trì
                maintenanceRequest.IsCustomerConfirmed = true; // Đánh dấu yêu cầu đã được xác nhận
                maintenanceRequest.UpdatedAt = DateTime.UtcNow;
                maintenanceRequest.Status = "Completed";

                // Thêm phản hồi nếu có
                if (!string.IsNullOrWhiteSpace(command.CustomerFeedback))
                {
                    var feedback = new CustomerFeedback
                    {
                        Id = Guid.NewGuid(),
                        MaintenanceRequestId = command.MaintenanceRequestId,
                        Feedback = command.CustomerFeedback,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _unitOfWork.Repository.AddAsync(feedback);
                }

                // Lưu các thay đổi và cam kết transaction
                await _unitOfWork.SaveEntitiesAsync();
                await _unitOfWork.CommitAsync(transaction);

                return new ApiResponse<ConfirmMaintenanceCompletionByCustomerDTO>("Confirm request was success")
                {
                    Succeeded = true,
                };
            }
            catch (Exception)
            {
                // Nếu có lỗi xảy ra, rollback transaction
                await _unitOfWork.RollbackAsync(transaction);
                return new ApiResponse<ConfirmMaintenanceCompletionByCustomerDTO>("Something go wrong ! Try again")
                {
                    Succeeded = false,
                };
            }
        }
    }
}
