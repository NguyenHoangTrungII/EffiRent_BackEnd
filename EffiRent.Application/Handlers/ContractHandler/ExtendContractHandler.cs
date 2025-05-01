// EffiRent.Application/Handlers/ContractHandler/ExtendContractHandler.cs
using EffiRent.Application.Commands.ContractCommand;
using EffiRent.Domain.Entities;
using EffiRent.Application.Services.Email;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore.Storage;
using EffiAP.Application.Queries;
using EffiRent.Domain.Entities;
using EffiAP.Infrastructure.IRepositories;

namespace EffiRent.Application.Handlers.ContractHandler
{
    public class ExtendContractHandler : BaseQuery, IRequestHandler<ExtendContractCommand, Guid>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;
        private readonly UserManager<IdentityUser> _userManager;

        public ExtendContractHandler(IUnitOfWork unitOfWork, IEmailService emailService, UserManager<IdentityUser> userManager, IConfiguration configuration)
            : base(configuration)
        {
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _userManager = userManager;
        }

        public async Task<Guid> Handle(ExtendContractCommand request, CancellationToken cancellationToken)
        {
            // Bắt đầu giao dịch
            IDbContextTransaction transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Lấy Contract
                var contract = await _unitOfWork.Repository.GetByIdAsync<Contract>(request.ContractId);
                if (contract == null) throw new Exception("Contract not found.");
                if (contract.Status != "Active") throw new Exception("Contract is not active.");

                // Lấy Tenant
                var tenant = await _userManager.FindByIdAsync(contract.TenantId);
                if (tenant == null) throw new Exception("Tenant not found.");

                // Lấy Room
                var room = await _unitOfWork.Repository.GetByIdAsync<Room>(contract.TenantRoomId);
                if (room == null) throw new Exception("Room not found.");
                if (room.Status != Room.RoomStatus.Occupied) throw new Exception("Room is not occupied.");

                // Kiểm tra ngày gia hạn
                if (request.NewEndDate <= contract.EndDate) throw new Exception("New end date must be later than current end date.");

                // Cập nhật Contract
                contract.EndDate = request.NewEndDate;
                await _unitOfWork.Repository.UpdateAsync<Contract>(contract);

                // Cập nhật Room
                room.EndDate = request.NewEndDate;
                await _unitOfWork.Repository.UpdateAsync<Room>(room);

                // Tạo Payment cho tháng tiếp theo
                var payment = new Payment
                {
                    Id = Guid.NewGuid(),
                    TenantId = contract.TenantId,
                    Amount = contract.RentAmount,
                    PaymentDate = DateTime.UtcNow,
                    PaymentMethod = request.PaymentMethod,
                    Status = "Pending",
                    Notes = $"Payment for extended contract {contract.Id} (Next Month)"
                };
                await _unitOfWork.Repository.AddAsync<Payment>(payment);

                // Liên kết Payment với Contract
                contract.Payments ??= new List<Payment>(); // Đảm bảo Payments không null
                contract.Payments.Add(payment);

                // Tạo Notification
                var notification = new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = contract.TenantId,
                    Title = "Contract Extended",
                    Message = $"Your contract (ID: {contract.Id}) for Room {room.Name} has been extended to {request.NewEndDate:yyyy-MM-dd}. Please pay {contract.RentAmount} via {request.PaymentMethod} for the next month.",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false,
                    RelatedEntityType = "Contract",
                    RelatedEntityId = contract.Id
                };
                await _unitOfWork.Repository.AddAsync<Notification>(notification);

                // Lưu tất cả thay đổi
                await _unitOfWork.SaveChangesAsync();

                // Commit giao dịch
                await _unitOfWork.CommitAsync(transaction);

                // Gửi email thông báo
                await _emailService.SendEmailAsync(
                    tenant.Email,
                    notification.Title,
                    notification.Message
                );

                return contract.Id;
            }
            catch (Exception ex)
            {
                // Rollback giao dịch nếu có lỗi
                await _unitOfWork.RollbackAsync(transaction);
                throw new Exception("Failed to extend contract: " + ex.Message, ex);
            }
            finally
            {
                // Giải phóng transaction
                transaction.Dispose();
            }
        }
    }
}