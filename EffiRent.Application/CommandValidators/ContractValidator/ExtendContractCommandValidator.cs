// EffiRent.Application/CommandValidators/ContractValidator.cs
using EffiRent.Application.Commands.ContractCommand;
using FluentValidation;

namespace EffiRent.Application.CommandValidators
{
    public class ExtendContractCommandValidator : AbstractValidator<ExtendContractCommand>
    {
        public ExtendContractCommandValidator()
        {
            RuleFor(x => x.ContractId).NotEmpty().WithMessage("Contract ID is required.");
            RuleFor(x => x.NewEndDate).NotEmpty().GreaterThan(DateTime.UtcNow).WithMessage("New end date must be in the future.");
            RuleFor(x => x.PaymentMethod).NotEmpty().Must(m => new[] { "Cash", "BankTransfer", "CreditCard" }.Contains(m))
                .WithMessage("Invalid payment method.");
        }
    }
}