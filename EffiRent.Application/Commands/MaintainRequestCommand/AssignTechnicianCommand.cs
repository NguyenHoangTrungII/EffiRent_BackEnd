using EffiAP.Domain.ViewModels.MaintainRequest;
using EffiAP.Application.Wrappers;
using EffiRent.Application.Services.Rabbit;
using MediatR;

namespace EffiAP.Application.Commands.MaintainRequestCommand
{
    public class AssignTechnicianCommand : IRequest<ApiResponse<MaintenanceRequestCommandDTO>>
    {
        //public MaintenanceMessage Message { get; set; }

        //public AssignTechnicianCommand(MaintenanceMessage message)
        //{
        //    Message = message;
        //}

        public RabbitMaintenanceMessage Message { get; set; }

        public AssignTechnicianCommand(RabbitMaintenanceMessage message)
        {
            Message = message;
        }

        
    }
}

//using EffiAP.Application.Handlers.MaintainRequestHandler;
//using EffiAP.Application.Wrappers;
//using EffiAP.Domain.ViewModels.MaintainRequest;
//using MediatR;
//using Microsoft.AspNetCore.Http;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace EffiAP.Application.Commands.MaintainRequestCommand
//{
//    public class AssignTechnicianCommand : IRequest<ApiResponse<MaintenanceRequestCommandDTO>>
//    {
//        public MaintenanceRequestCommandDTO RequestDto { get; set; }
//        public List<IFormFile> Image { get; set; }



//        public AssignTechnicianCommand(MaintenanceRequestCommandDTO requestDto, List<IFormFile> image = null)
//        {
//            RequestDto = requestDto;
//            Image = image;
//        }

//        public AssignTechnicianCommand(MaintenanceMessage maintenance)
//        {
//            RequestDto = maintenance.request;
//            Image = maintenance.FileBase64;
//        }
//    }

//}
