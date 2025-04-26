using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// EffiRent.Application/Commands/ContractCommand/CreateContractCommand.cs
namespace EffiRent.Application.Commands.ContractCommand
{
    public class CreateContractCommand: IRequest<Guid>
    {
        public string TenantId { get; set; }
        public Guid RoomId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal RentAmount { get; set; }
        public decimal DepositAmount { get; set; }
        public string Terms { get; set; }
        public string PaymentMethod { get; set; } // Phương thức thanh toán ban đầu
    }
}
