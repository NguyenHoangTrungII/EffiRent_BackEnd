using FluentValidation;
using EffiAP.Application.Commands.MaintainRequestCommand;

namespace EffiAP.Application.CommandValidators
{
    public class AssignTechnicianValidator : AbstractValidator<AssignTechnicianCommand>
    {
        public AssignTechnicianValidator()
        {
            // Ensure RequestDto is not null
            RuleFor(x => x.Message.request)
                .NotNull().WithMessage("RequestDto is required.")
                .WithErrorCode("1001");

            // TechnicianId can be null but if provided, it must not be empty
            RuleFor(x => x.Message.request.TechnicianId)
                .Must(id => id == null || !string.IsNullOrWhiteSpace(id))
                .WithMessage("TechnicianId cannot be empty if provided.")
                .WithErrorCode("1002");

            // RoomId should not be an empty GUID
            RuleFor(x => x.Message.request.RoomId)
                .NotEmpty().WithMessage("RoomId is required.")
                .WithErrorCode("1003");

            // CustomerId should not be null or empty
            RuleFor(x => x.Message.request.CustomerId)
                .NotNull().WithMessage("CustomerId is required.")
                .NotEmpty().WithMessage("CustomerId cannot be empty.")
                .WithErrorCode("1004");

            // CategoryId should not be an empty GUID
            RuleFor(x => x.Message.request.CategoryId)
                .NotEmpty().WithMessage("CategoryId is required.")
                .WithErrorCode("1005");

            // PriorityLevel should be between a valid range (1-5)
            RuleFor(x => x.Message.request.PriorityLevel)
                .InclusiveBetween(1, 5).WithMessage("PriorityLevel must be between 1 and 5.")
                .WithErrorCode("1006");

            // Status should not be null or empty
            RuleFor(x => x.Message.request.Status)
                .NotNull().WithMessage("Status is required.")
                .NotEmpty().WithMessage("Status cannot be empty.")
                .WithErrorCode("1007");

            // CreatedAt should be a valid date in the past or present
            //RuleFor(x => x.RequestDto.CreatedAt)
            //    .LessThanOrEqualTo(DateTime.Now)
            //    .WithMessage("CreatedAt must be a date in the past or present.")
            //    .WithErrorCode("1008");

            // CustomerFeedback can be null but must not be empty if provided
            RuleFor(x => x.Message.request.CustomerFeedback)
                .Must(feedback => feedback == null || !string.IsNullOrWhiteSpace(feedback))
                .WithMessage("CustomerFeedback cannot be empty if provided.")
                .WithErrorCode("1009");
        }
    }
}
