using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Application.Commands.BranchCommand
{
    public class DeleteBranchCommand : IRequest
    {
        public Guid BranchID { get; set; }

        public DeleteBranchCommand(Guid branchId)
        {
            BranchID = branchId;
        }
    }
}
