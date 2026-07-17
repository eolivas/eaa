using FluentValidation;

namespace Orders.Application.Commands;

/// <summary>
/// Validates PlaceOrderCommand ensuring required fields are present.
/// </summary>
public class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(x => x.Lines)
            .NotEmpty()
            .WithMessage("An order must contain at least one line.");
    }
}
