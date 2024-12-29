using EffiAP.Application.Wrappers;
using EffiAP.Domain.ViewModels.MaintainRequest;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Application.Commands.MaintainRequestCommand
{
    public class CompleteMaintenanceCommand : IRequest<ApiResponse<CompleteMaintenanceRequestDTO>>
    {
        public CompleteMaintenanceRequestDTO CompleteRequestDto { get; }

        public CompleteMaintenanceCommand(CompleteMaintenanceRequestDTO completeRequestDto)
        {
            CompleteRequestDto = completeRequestDto;
        }
    }

}
