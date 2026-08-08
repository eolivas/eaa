using FluentValidation;

namespace Orders.Application.Commands;

/// <summary>
/// Validates PlaceOrderCommand ensuring required fields are present and order lines are valid.
/// </summary>
public class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(x => x.Lines)
            .NotEmpty()
            .WithMessage("An order must contain at least one line.");

        RuleForEach(x => x.Lines)
            .ChildRules(line =>
            {
                line.RuleFor(l => l.Quantity)
                    .GreaterThanOrEqualTo(1)
                    .WithMessage("Quantity must be at least 1.");

                line.RuleFor(l => l.UnitPrice)
                    .GreaterThan(0)
                    .WithMessage("UnitPrice must be greater than zero.");
            });
    }
}
