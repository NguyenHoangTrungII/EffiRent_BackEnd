// EffiRent.Application/CommandValidators/ContractValidator.cs
using EffiRent.Application.Commands.ContractCommand;
using FluentValidation;

namespace EffiRent.Application.CommandValidators
{
    public class TransferRoomCommandValidator : AbstractValidator<TransferRoomCommand>
    {
        public TransferRoomCommandValidator()
        {
            RuleFor(x => x.OldContractId).NotEmpty().WithMessage("Old Contract ID is required.");
            RuleFor(x => x.NewRoomId).NotEmpty().WithMessage("New Room ID is required.");
            RuleFor(x => x.NewStartDate).NotEmpty().GreaterThanOrEqualTo(DateTime.UtcNow).WithMessage("New start date must be today or in the future.");
            RuleFor(x => x.NewEndDate).NotEmpty().GreaterThan(x => x.NewStartDate).WithMessage("New end date must be after new start date.");
            RuleFor(x => x.PaymentMethod).NotEmpty().Must(m => new[] { "Cash", "BankTransfer", "CreditCard" }.Contains(m))
                .WithMessage("Invalid payment method.");
        }
    }
}