using FluentValidation;
using EffiAP.Application.Commands.BranchCommand;
using EffiAP.Application.Commands.BranchCommand;

namespace EffiAP.Application.CommandValidators
{
    public class UpdateBranchValidator : AbstractValidator<UpdateBranchCommand>
    {
        public UpdateBranchValidator()
        {
            RuleFor(x => x.BranchID)
                .NotNull().WithMessage("BranchID is required.")
                .NotEmpty().WithMessage("BranchID cannot be empty.")
                .WithErrorCode("0001"); // Mã lỗi tùy chỉnh

            RuleFor(x => x.OwnerId)
                .NotNull().WithMessage("OwnerId is required.")
                .NotEmpty().WithMessage("OwnerId cannot be empty.")
                .WithErrorCode("0002"); // Mã lỗi tùy chỉnh

            RuleFor(x => x.BranchName)
                .NotNull().WithMessage("BranchName is required.")
                .NotEmpty().WithMessage("BranchName cannot be empty.")
                .WithErrorCode("0003"); // Mã lỗi tùy chỉnh

            RuleFor(x => x.Address)
                .NotNull().WithMessage("Address is required.")
                .NotEmpty().WithMessage("Address cannot be empty.")
                .WithErrorCode("0004"); // Mã lỗi tùy chỉnh

            RuleFor(x => x.Phone)
                .NotNull().WithMessage("Phone is required.")
                .NotEmpty().WithMessage("Phone cannot be empty.")
                .Matches(@"^\d{10,15}$").WithMessage("Phone must be between 10 to 15 digits.")
                .WithErrorCode("0005"); // Mã lỗi tùy chỉnh

            RuleFor(x => x.Email)
                .NotNull().WithMessage("Email is required.")
                .NotEmpty().WithMessage("Email cannot be empty.")
                .EmailAddress().WithMessage("Invalid email format.")
                .WithErrorCode("0006"); // Mã lỗi tùy chỉnh
        }
    }
}
