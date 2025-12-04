using FluentValidation;

namespace CoralLedger.Application.Features.Observations.Commands.CreateObservation;

public class CreateObservationCommandValidator : AbstractValidator<CreateObservationCommand>
{
    public CreateObservationCommandValidator()
    {
        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180)
            .WithMessage("Longitude must be between -180 and 180");

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90)
            .WithMessage("Latitude must be between -90 and 90");

        RuleFor(x => x.ObservationTime)
            .NotEmpty()
            .LessThanOrEqualTo(DateTime.UtcNow.AddHours(1))
            .WithMessage("Observation time cannot be in the future");

        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Title is required and must be 200 characters or less");

        RuleFor(x => x.Description)
            .MaximumLength(2000)
            .When(x => x.Description != null)
            .WithMessage("Description must be 2000 characters or less");

        RuleFor(x => x.Severity)
            .InclusiveBetween(1, 5)
            .WithMessage("Severity must be between 1 (low) and 5 (critical)");

        RuleFor(x => x.CitizenEmail)
            .EmailAddress()
            .When(x => !string.IsNullOrEmpty(x.CitizenEmail))
            .WithMessage("Invalid email address format");

        RuleFor(x => x.CitizenName)
            .MaximumLength(100)
            .When(x => x.CitizenName != null)
            .WithMessage("Name must be 100 characters or less");

        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Invalid observation type");
    }
}
