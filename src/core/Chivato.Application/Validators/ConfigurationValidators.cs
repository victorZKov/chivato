using Chivato.Application.Commands.Configuration;
using FluentValidation;

namespace Chivato.Application.Validators;

public class UpdateTimerCommandValidator : AbstractValidator<UpdateTimerCommand>
{
    public UpdateTimerCommandValidator()
    {
        RuleFor(x => x.IntervalHours)
            .InclusiveBetween(1, 168)
            .WithMessage("Interval must be between 1 and 168 hours (1 week)");
    }
}

public class UpdateSettingsCommandValidator : AbstractValidator<UpdateSettingsCommand>
{
    private static readonly string[] ValidSeverities = { "Low", "Medium", "High", "Critical" };

    public UpdateSettingsCommandValidator()
    {
        RuleFor(x => x.MinimumSeverityForAlert)
            .Must(x => ValidSeverities.Contains(x))
            .WithMessage("Severity must be one of: Low, Medium, High, Critical");

        RuleFor(x => x.MaxConcurrentScans)
            .InclusiveBetween(1, 10)
            .WithMessage("Max concurrent scans must be between 1 and 10");

        RuleFor(x => x.RetentionDays)
            .InclusiveBetween(7, 365)
            .WithMessage("Retention must be between 7 and 365 days");
    }
}

public class SaveAzureConnectionCommandValidator : AbstractValidator<SaveAzureConnectionCommand>
{
    public SaveAzureConnectionCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(100);

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Tenant ID is required")
            .Must(BeValidGuid).WithMessage("Tenant ID must be a valid GUID");

        RuleFor(x => x.SubscriptionId)
            .NotEmpty().WithMessage("Subscription ID is required")
            .Must(BeValidGuid).WithMessage("Subscription ID must be a valid GUID");

        RuleFor(x => x.ClientId)
            .NotEmpty().WithMessage("Client ID is required")
            .Must(BeValidGuid).WithMessage("Client ID must be a valid GUID");

        RuleFor(x => x.ClientSecret)
            .NotEmpty().WithMessage("Client Secret is required")
            .MinimumLength(10).WithMessage("Client Secret seems too short");
    }

    private static bool BeValidGuid(string value) => Guid.TryParse(value, out _);
}

public class SaveAdoConnectionCommandValidator : AbstractValidator<SaveAdoConnectionCommand>
{
    public SaveAdoConnectionCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(100);

        RuleFor(x => x.Organization)
            .NotEmpty().WithMessage("Organization is required")
            .MaximumLength(100)
            .Matches(@"^[a-zA-Z0-9-]+$").WithMessage("Organization can only contain letters, numbers, and hyphens");

        RuleFor(x => x.Project)
            .NotEmpty().WithMessage("Project is required")
            .MaximumLength(100);

        RuleFor(x => x.PatToken)
            .NotEmpty().WithMessage("PAT Token is required")
            .MinimumLength(20).WithMessage("PAT Token seems too short");
    }
}

public class AddEmailRecipientCommandValidator : AbstractValidator<AddEmailRecipientCommand>
{
    private static readonly string[] ValidSeverities = { "Low", "Medium", "High", "Critical" };

    public AddEmailRecipientCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(100);

        RuleFor(x => x.MinimumSeverity)
            .Must(x => ValidSeverities.Contains(x))
            .WithMessage("Severity must be one of: Low, Medium, High, Critical");
    }
}
