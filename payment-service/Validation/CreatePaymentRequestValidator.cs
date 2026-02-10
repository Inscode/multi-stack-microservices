using FluentValidation;
using PaymentService.Contracts;

namespace PaymentService.Validation;

public class CreatePaymentRequestValidator : AbstractValidator<CreatePaymentRequest>
{
    public CreatePaymentRequestValidator()
    {
        RuleFor(x => x.OrderId)
        .GreaterThan(0)
        .WithMessage("OrderId must be greater that 0");

        RuleFor(x => x.Amount)
        .GreaterThan(0)
        .WithMessage("Amount must be greater than 0");
    }
}