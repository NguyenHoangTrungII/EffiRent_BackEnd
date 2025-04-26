// EffiRent.Application/CommandValidators/RoomValidator.cs
using EffiRent.Application.Commands.RoomCommand;
using FluentValidation;

namespace EffiRent.Application.CommandValidators
{
    public class CreateRoomCommandValidator : AbstractValidator<CreateRoomCommand>
    {
        public CreateRoomCommandValidator()
        {
            RuleFor(x => x.BranchID).NotEmpty().WithMessage("Branch ID is required.");
            RuleFor(x => x.Name).NotEmpty().MaximumLength(50).WithMessage("Room name is required and must be less than 50 characters.");
            RuleFor(x => x.Location).NotEmpty().MaximumLength(100).WithMessage("Location is required and must be less than 100 characters.");
            RuleFor(x => x.Status).IsInEnum().WithMessage("Invalid room status.");
            RuleFor(x => x.StartDate).LessThanOrEqualTo(x => x.EndDate).When(x => x.StartDate.HasValue && x.EndDate.HasValue)
                .WithMessage("Start date must be before or equal to end date.");
        }
    }
}