using CatalogAPI.Application.Commands;
using FluentValidation;

namespace CatalogAPI.Application.Validators;

public class PurchaseGameCommandValidator : AbstractValidator<PurchaseGameCommand>
{
    public PurchaseGameCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x.CorrelationId)
            .NotEmpty()
            .WithMessage("CorrelationId is required.");
    }
}
