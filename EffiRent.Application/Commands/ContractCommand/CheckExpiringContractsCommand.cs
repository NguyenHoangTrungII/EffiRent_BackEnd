// EffiRent.Application/Commands/ContractCommand/CheckExpiringContractsCommand.cs
using MediatR;

namespace EffiRent.Application.Commands.ContractCommand
{
    public class CheckExpiringContractsCommand:IRequest<Unit>
    {
        public DateTime CheckDate { get; set; } // Ngày kiểm tra
    }
}
