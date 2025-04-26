// EffiRent.Application/Handlers/ContractHandler/CheckExpiringContractsHandler.cs
using EffiRent.Application.Commands.ContractCommand;
using EffiRent.Domain.Entities;
using EffiRent.Application.Services.Email;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore.Storage;
using EffiAP.Application.Queries;
using EffiAP.Infrastructure.IRepositories;

namespace EffiRent.Application.Handlers.ContractHandler
{
    public class CheckExpiringContractsHandler : BaseQuery, IRequestHandler<CheckExpiringContractsCommand, Unit>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;
        private readonly UserManager<IdentityUser> _userManager;

        public CheckExpiringContractsHandler(IUnitOfWork unitOfWork, IEmailService emailService, UserManager<IdentityUser> userManager, IConfiguration configuration)
            : base(configuration)
        {
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _userManager = userManager;
        }

        public async Task<Unit> Handle(CheckExpiringContractsCommand request, CancellationToken cancellationToken)
        {
            // Lấy các hợp đồng sắp hết hạn trong 7 ngày tới
            var expiringContracts =  _unitOfWork.Repository
                .Get<Contract>(c => c.Status == "Active" &&
                               c.EndDate >= request.CheckDate &&
                               c.EndDate <= request.CheckDate.AddDays(7));

            foreach (var contract in expiringContracts)
            {
                // Bắt đầu giao dịch cho mỗi hợp đồng
                IDbContextTransaction transaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    // Lấy Tenant
                    var tenant = await _userManager.FindByIdAsync(contract.TenantId);
                    if (tenant == null) throw new Exception($"Tenant not found for contract {contract.Id}.");

                    // Kiểm tra xem đã gửi thông báo cho ngày hết hạn này chưa
                    var existingNotification =  _unitOfWork.Repository
                        .Get<Notification>(n => n.UserId == contract.TenantId &&
                                       n.RelatedEntityId == contract.Id &&
                                       n.CreatedAt.Date == request.CheckDate.Date &&
                                       n.RelatedEntityType == "Contract");
                    if (existingNotification.Any())
                    {
                        // Bỏ qua nếu đã gửi thông báo hôm nay
                        await _unitOfWork.CommitTransactionAsync(transaction);
                        continue;
                    }

                    // Tạo Notification
                    var notification = new Notification
                    {
                        Id = Guid.NewGuid(),
                        UserId = contract.TenantId,
                        Title = "Contract Expiring Soon",
                        Message = $"Your contract (ID: {contract.Id}) for Room {contract.TenantRoom.Room} will expire on {contract.EndDate:yyyy-MM-dd}. Please renew or contact us.",
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false,
                        RelatedEntityType = "Contract",
                        RelatedEntityId = contract.Id
                    };
                    await _unitOfWork.Repository.AddAsync<Notification>(notification);

                    // Lưu thay đổi
                    await _unitOfWork.SaveChangesAsync();

                    // Commit giao dịch
                    await _unitOfWork.CommitTransactionAsync(transaction);

                    // Gửi email thông báo
                    await _emailService.SendEmailAsync(
                        tenant.Email,
                        notification.Title,
                        notification.Message
                    );
                }
                catch (Exception ex)
                {
                    // Rollback giao dịch nếu có lỗi
                    await transaction.RollbackAsync();
                    throw new Exception($"Failed to process expiring contract {contract.Id}: {ex.Message}", ex);
                }
                finally
                {
                    transaction.Dispose();
                }
            }

            return Unit.Value;
        }
    }
}