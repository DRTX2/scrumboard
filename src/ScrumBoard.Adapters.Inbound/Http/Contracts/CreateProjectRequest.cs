using System.ComponentModel.DataAnnotations;
using ScrumBoard.Domain.Projects;

namespace ScrumBoard.Adapters.Inbound.Http.Contracts;

public sealed record CreateProjectRequest(
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(160, ErrorMessage = "El nombre no puede superar 160 caracteres.")]
    string Name,
    [StringLength(2_000, ErrorMessage = "La descripción no puede superar 2000 caracteres.")]
    string? Description,
    DateOnly StartDate,
    DateOnly ExpectedEndDate,
    ProjectStatus Status) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate == default) yield return new ValidationResult("La fecha de inicio es obligatoria.", [nameof(StartDate)]);
        if (ExpectedEndDate == default) yield return new ValidationResult("La fecha prevista de fin es obligatoria.", [nameof(ExpectedEndDate)]);
        if (ExpectedEndDate < StartDate)
        {
            yield return new ValidationResult(
                "La fecha prevista de fin no puede ser anterior a la fecha de inicio.",
                [nameof(ExpectedEndDate)]);
        }

        if (!Enum.IsDefined(Status)) yield return new ValidationResult("El estado no es válido.", [nameof(Status)]);
    }
}
