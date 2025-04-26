// EffiRent.Application/CommandValidators/ContractValidator.cs
using EffiRent.Application.Commands.ContractCommand;
using FluentValidation;

namespace EffiRent.Application.CommandValidators
{
    public class CreateContractCommandValidator : AbstractValidator<CreateContractCommand>
    {
        public CreateContractCommandValidator()
        {
            RuleFor(x => x.TenantId).NotEmpty().WithMessage("Tenant ID is required.");
            RuleFor(x => x.RoomId).NotEmpty().WithMessage("Room ID is required.");
            RuleFor(x => x.StartDate).NotEmpty().LessThanOrEqualTo(x => x.EndDate).WithMessage("Start date must be before or equal to end date.");
            RuleFor(x => x.EndDate).NotEmpty().WithMessage("End date is required.");
            RuleFor(x => x.RentAmount).GreaterThan(0).WithMessage("Rent amount must be greater than 0.");
            RuleFor(x => x.DepositAmount).GreaterThanOrEqualTo(0).WithMessage("Deposit amount must be non-negative.");
            RuleFor(x => x.Terms).NotEmpty().MaximumLength(1000).WithMessage("Terms are required and must be less than 1000 characters.");
            RuleFor(x => x.PaymentMethod).NotEmpty().Must(m => new[] { "Cash", "BankTransfer", "CreditCard" }.Contains(m))
                .WithMessage("Invalid payment method.");
        }
    }
}