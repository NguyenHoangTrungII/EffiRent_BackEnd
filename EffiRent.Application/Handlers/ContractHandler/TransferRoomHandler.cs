// EffiRent.Application/Handlers/ContractHandler/TransferRoomHandler.cs
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
    public class TransferRoomHandler : BaseQuery, IRequestHandler<TransferRoomCommand, Guid>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;
        private readonly UserManager<IdentityUser> _userManager;

        public TransferRoomHandler(IUnitOfWork unitOfWork, IEmailService emailService, UserManager<IdentityUser> userManager, IConfiguration configuration)
            : base(configuration)
        {
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _userManager = userManager;
        }

        public async Task<Guid> Handle(TransferRoomCommand request, CancellationToken cancellationToken)
        {
            // Bắt đầu giao dịch
            IDbContextTransaction transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Lấy hợp đồng cũ
                var oldContract = await _unitOfWork.Repository.GetByIdAsync<Contract>(request.OldContractId);
                if (oldContract == null) throw new Exception("Old contract not found.");
                if (oldContract.Status != "Active") throw new Exception("Old contract is not active.");

                // Lấy Tenant
                var tenant = await _userManager.FindByIdAsync(oldContract.TenantId);
                if (tenant == null) throw new Exception("Tenant not found.");

                // Lấy phòng cũ
                var oldRoom = await _unitOfWork.Repository.GetByIdAsync<Room>(oldContract.TenantRoomId);
                if (oldRoom == null) throw new Exception("Old room not found.");

                // Lấy phòng mới
                var newRoom = await _unitOfWork.Repository.GetByIdAsync<Room>(request.NewRoomId);
                if (newRoom == null) throw new Exception("New room not found.");
                if (newRoom.Status != Room.RoomStatus.Available) throw new Exception("New room is not available.");

                // Kết thúc hợp đồng cũ
                oldContract.Status = "Terminated";
                oldContract.EndDate = DateTime.UtcNow; // Kết thúc ngay lập tức
                await _unitOfWork.Repository.UpdateAsync<Contract>(oldContract);

                // Cập nhật trạng thái phòng cũ
                oldRoom.Status = Room.RoomStatus.Available;
                oldRoom.EndDate = DateTime.UtcNow;
                await _unitOfWork.Repository.UpdateAsync<Room>(oldRoom);

                // Tạo hợp đồng mới
                var newContract = new Contract
                {
                    Id = Guid.NewGuid(),
                    TenantId = oldContract.TenantId,
                    TenantRoomId = request.NewRoomId,
                    StartDate = request.NewStartDate,
                    EndDate = request.NewEndDate,
                    RentAmount = oldContract.RentAmount, // Giả sử Room có RentAmount, nếu không thì dùng giá cũ
                    DepositAmount = oldContract.DepositAmount, // Giữ nguyên tiền đặt cọc
                    Terms = oldContract.Terms, // Giữ nguyên điều khoản
                    Status = "Active",
                    Payments = new List<Payment>()
                };
                await _unitOfWork.Repository.AddAsync<Contract>(newContract);

                // Cập nhật trạng thái phòng mới
                newRoom.Status = Room.RoomStatus.Occupied;
                newRoom.StartDate = request.NewStartDate;
                newRoom.EndDate = request.NewEndDate;
                await _unitOfWork.Repository.UpdateAsync<Room>(newRoom);

                // Tính toán điều chỉnh thanh toán (giả sử đơn giản: hoàn tiền hoặc tính thêm dựa trên RentAmount)
                decimal paymentAdjustment = newContract.RentAmount - oldContract.RentAmount;
                var payment = new Payment
                {
                    Id = Guid.NewGuid(),
                    TenantId = oldContract.TenantId,
                    Amount = paymentAdjustment,
                    PaymentDate = DateTime.UtcNow,
                    PaymentMethod = request.PaymentMethod,
                    Status = "Pending",
                    Notes = paymentAdjustment >= 0
                        ? $"Additional payment for room transfer to Contract {newContract.Id}"
                        : $"Refund for room transfer from Contract {oldContract.Id}"
                };
                await _unitOfWork.Repository.AddAsync<Payment>(payment);
                newContract.Payments.Add(payment);

                // Tạo thông báo
                var notification = new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = oldContract.TenantId,
                    Title = "Room Transfer Completed",
                    Message = paymentAdjustment >= 0
                        ? $"You have transferred from Room {oldRoom.Name} to Room {newRoom.Name}. New contract (ID: {newContract.Id}) created. Please pay {paymentAdjustment} via {request.PaymentMethod}."
                        : $"You have transferred from Room {oldRoom.Name} to Room {newRoom.Name}. New contract (ID: {newContract.Id}) created. You will be refunded {-paymentAdjustment} via {request.PaymentMethod}.",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false,
                    RelatedEntityType = "Contract",
                    RelatedEntityId = newContract.Id
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

                return newContract.Id; // Trả về ID của hợp đồng mới
            }
            catch (Exception ex)
            {
                // Rollback giao dịch nếu có lỗi
                await transaction.RollbackAsync();
                throw new Exception("Failed to transfer room: " + ex.Message, ex);
            }
            finally
            {
                // Giải phóng transaction
                transaction.Dispose();
            }
        }
    }
}