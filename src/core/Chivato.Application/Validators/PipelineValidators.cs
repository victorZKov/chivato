using Chivato.Application.Commands.Pipelines;
using FluentValidation;

namespace Chivato.Application.Validators;

public class CreatePipelineCommandValidator : AbstractValidator<CreatePipelineCommand>
{
    public CreatePipelineCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters");

        RuleFor(x => x.Organization)
            .NotEmpty().WithMessage("Organization is required")
            .MaximumLength(100).WithMessage("Organization cannot exceed 100 characters");

        RuleFor(x => x.Project)
            .NotEmpty().WithMessage("Project is required")
            .MaximumLength(100).WithMessage("Project cannot exceed 100 characters");

        RuleFor(x => x.RepositoryId)
            .NotEmpty().WithMessage("Repository ID is required");

        RuleFor(x => x.Branch)
            .NotEmpty().WithMessage("Branch is required")
            .MaximumLength(100).WithMessage("Branch cannot exceed 100 characters");

        RuleFor(x => x.TerraformPath)
            .NotEmpty().WithMessage("Terraform path is required")
            .MaximumLength(500).WithMessage("Terraform path cannot exceed 500 characters");

        RuleFor(x => x.SubscriptionId)
            .NotEmpty().WithMessage("Subscription ID is required")
            .Must(BeValidGuid).WithMessage("Subscription ID must be a valid GUID");

        RuleFor(x => x.ResourceGroup)
            .NotEmpty().WithMessage("Resource group is required")
            .MaximumLength(90).WithMessage("Resource group cannot exceed 90 characters");
    }

    private static bool BeValidGuid(string value)
    {
        return Guid.TryParse(value, out _);
    }
}

public class UpdatePipelineCommandValidator : AbstractValidator<UpdatePipelineCommand>
{
    public UpdatePipelineCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Pipeline ID is required");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters");

        RuleFor(x => x.Branch)
            .NotEmpty().WithMessage("Branch is required")
            .MaximumLength(100).WithMessage("Branch cannot exceed 100 characters");

        RuleFor(x => x.TerraformPath)
            .NotEmpty().WithMessage("Terraform path is required")
            .MaximumLength(500).WithMessage("Terraform path cannot exceed 500 characters");

        RuleFor(x => x.SubscriptionId)
            .NotEmpty().WithMessage("Subscription ID is required")
            .Must(BeValidGuid).WithMessage("Subscription ID must be a valid GUID");

        RuleFor(x => x.ResourceGroup)
            .NotEmpty().WithMessage("Resource group is required")
            .MaximumLength(90).WithMessage("Resource group cannot exceed 90 characters");
    }

    private static bool BeValidGuid(string value)
    {
        return Guid.TryParse(value, out _);
    }
}

public class DeletePipelineCommandValidator : AbstractValidator<DeletePipelineCommand>
{
    public DeletePipelineCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Pipeline ID is required");
    }
}

public class ScanPipelineCommandValidator : AbstractValidator<ScanPipelineCommand>
{
    public ScanPipelineCommandValidator()
    {
        RuleFor(x => x.PipelineId)
            .NotEmpty().WithMessage("Pipeline ID is required");
    }
}
