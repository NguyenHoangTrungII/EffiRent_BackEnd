using EffiAP.Application.Wrappers;
using EffiAP.Domain.ViewModels.MaintainRequest;
using MediatR;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Application.Commands.MaintainRequestCommand
{
    public class AssignTechnicianCommand : IRequest<ApiResponse<MaintenanceRequestCommandDTO>>
    {
        public MaintenanceRequestCommandDTO RequestDto { get; set; }
        public List<IFormFile> Image { get; set; }



        public AssignTechnicianCommand(MaintenanceRequestCommandDTO requestDto, List<IFormFile> image = null)
        {
            RequestDto = requestDto;
            Image = image;
        }
    }

}
