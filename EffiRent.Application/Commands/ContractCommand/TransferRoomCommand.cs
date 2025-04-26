// EffiRent.Application/Commands/ContractCommand/TransferRoomCommand.cs
using MediatR;

namespace EffiRent.Application.Commands.ContractCommand
{
    public class TransferRoomCommand : IRequest<Guid>
    {
        public Guid OldContractId { get; set; } // Hợp đồng hiện tại
        public Guid NewRoomId { get; set; } // Phòng mới
        public DateTime NewStartDate { get; set; } // Ngày bắt đầu hợp đồng mới
        public DateTime NewEndDate { get; set; } // Ngày kết thúc hợp đồng mới
        public string PaymentMethod { get; set; } // Phương thức thanh toán điều chỉnh
    }
}