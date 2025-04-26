using EffiAP.Application.Wrappers;
using EffiAP.Domain.ViewModels.MaintainRequest;
using MediatR;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiRent.Application.Commands.MaintainRequestCommand
{
    public class CreateMaintenanceRequestCommand : IRequest<ApiResponse<bool>>
    {
        public MaintenanceRequestCommandDTO RequestDto { get; set; }
        public List<IFormFile> Images { get; set; }

        public CreateMaintenanceRequestCommand(MaintenanceRequestCommandDTO requestDto, List<IFormFile> images)
        {
            RequestDto = requestDto;
            Images = images;
        }
    }
}
