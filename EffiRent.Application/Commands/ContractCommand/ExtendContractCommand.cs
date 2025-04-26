// EffiRent.Application/Commands/ContractCommand/ExtendContractCommand.cs
using MediatR;

namespace EffiRent.Application.Commands.ContractCommand
{
    public class ExtendContractCommand : IRequest<Guid>
    {
        public Guid ContractId { get; set; }
        public DateTime NewEndDate { get; set; }
        public string PaymentMethod { get; set; } // Phương thức thanh toán cho tháng tiếp theo
    }
}