// EffiRent.Application/Handlers/ContractHandler/CreateContractHandler.cs
using EffiRent.Application.Commands.ContractCommand;
using EffiRent.Domain.Entities;
using EffiRent.Application.Services.Email;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using EffiRent.Domain.Entities;
using EffiAP.Application.Queries;
using EffiAP.Infrastructure.IRepositories; // Thêm để dùng transaction

namespace EffiRent.Application.Handlers.ContractHandler
{
    public class CreateContractHandler : BaseQuery, IRequestHandler<CreateContractCommand, Guid>
    {
        private readonly EffiAP.Infrastructure.IRepositories.IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;
        private readonly UserManager<IdentityUser> _userManager;

        public CreateContractHandler(IUnitOfWork unitOfWork, IEmailService emailService, UserManager<IdentityUser> userManager, IConfiguration configuration)
            : base(configuration)
        {
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _userManager = userManager;
        }

        public async Task<Guid> Handle(CreateContractCommand request, CancellationToken cancellationToken)
        {
            // Kiểm tra Tenant bằng UserManager
            var tenant = await _userManager.FindByIdAsync(request.TenantId);
            if (tenant == null) throw new Exception("Tenant not found.");

            // Bắt đầu giao dịch để đảm bảo tính toàn vẹn
            using (var transaction = await _unitOfWork.BeginTransactionAsync())
            {
                try
                {
                    // Kiểm tra Room
                    var room = await _unitOfWork.Repository.GetByIdAsync<Room>(request.RoomId);
                    if (room == null) throw new Exception("Room not found.");
                    if (room.Status != Room.RoomStatus.Available) throw new Exception("Room is not available.");

                    // Tạo Contract
                    var contract = new Contract
                    {
                        Id = Guid.NewGuid(),
                        TenantId = request.TenantId, // Đồng bộ với tên thuộc tính
                        TenantRoomId = request.RoomId,
                        StartDate = request.StartDate,
                        EndDate = request.EndDate,
                        RentAmount = request.RentAmount,
                        DepositAmount = request.DepositAmount,
                        Terms = request.Terms,
                        Status = "Active",
                        Payments = new List<Payment>()
                    };

                    // Cập nhật trạng thái Room
                    room.Status = Room.RoomStatus.Occupied;
                    room.StartDate = request.StartDate;
                    room.EndDate = request.EndDate;

                    // Tạo Payment
                    var totalInitialPayment = request.DepositAmount + request.RentAmount;
                    var payment = new Payment
                    {
                        Id = Guid.NewGuid(),
                        TenantId = request.TenantId,
                        Amount = totalInitialPayment,
                        PaymentDate = DateTime.UtcNow,
                        PaymentMethod = request.PaymentMethod,
                        Status = "Pending",
                        Notes = $"Initial payment for Contract {contract.Id} (Deposit + First Month)"
                    };
                    contract.Payments.Add(payment);

                    // Tạo Notification
                    var notification = new Notification
                    {
                        Id = Guid.NewGuid(),
                        UserId = request.TenantId,
                        Title = "New Room Contract Created",
                        Message = $"Your contract (ID: {contract.Id}) for Room {room.Name} has been created. Please complete the initial payment of {totalInitialPayment} via {request.PaymentMethod}.",
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false,
                        RelatedEntityType = "Contract",
                        RelatedEntityId = contract.Id
                    };

                    // Thêm tất cả thay đổi vào Unit of Work
                    await _unitOfWork.Repository.UpdateAsync<Room>(room);
                    await _unitOfWork.Repository.AddAsync<Contract>(contract);
                    await _unitOfWork.Repository.AddAsync<Payment>(payment);
                    await _unitOfWork.Repository.AddAsync<Notification>(notification);

                    // Lưu tất cả thay đổi trong một giao dịch
                    await _unitOfWork.SaveChangesAsync();

                    // Commit giao dịch nếu mọi thứ thành công
                    await transaction.CommitAsync();

                    // Gửi email thông báo (ngoài giao dịch để tránh ảnh hưởng tính toàn vẹn dữ liệu)
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
                    await transaction.RollbackAsync();
                    throw new Exception("Failed to create contract: " + ex.Message, ex);
                }
            }
        }
    }
}