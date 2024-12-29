using EffiAP.Domain.ViewModels.MaintainRequest;
using FluentValidation;

namespace EffiAP.Application.CommandValidators.MaintainRequestValidator
{
   
    public class CompleteMaintenanceRequestDTOValidator : AbstractValidator<CompleteMaintenanceRequestDTO>
    {
        public CompleteMaintenanceRequestDTOValidator()
        {
            RuleFor(x => x.RequestId)
                .NotEmpty().WithMessage("Request ID is required.");

            RuleFor(x => x.TechnicianId)
                .NotEmpty().WithMessage("Technician ID is required.");
        }
    }

}
