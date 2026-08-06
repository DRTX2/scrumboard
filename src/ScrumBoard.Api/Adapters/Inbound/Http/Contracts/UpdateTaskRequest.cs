using System.ComponentModel.DataAnnotations;
using ScrumBoard.Domain.Tasks;

namespace ScrumBoard.Api.Adapters.Inbound.Http.Contracts;

public sealed record UpdateTaskRequest(
    [Required(ErrorMessage = "El título es obligatorio.")]
    [StringLength(200, ErrorMessage = "El título no puede superar 200 caracteres.")]
    string Title,
    [StringLength(4_000, ErrorMessage = "La descripción no puede superar 4000 caracteres.")]
    string? Description,
    TaskPriority Priority,
    Guid AssigneeId,
    DateOnly? DueDate) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AssigneeId == Guid.Empty) yield return new ValidationResult("La persona asignada es obligatoria.", [nameof(AssigneeId)]);
        if (!Enum.IsDefined(Priority)) yield return new ValidationResult("La prioridad no es válida.", [nameof(Priority)]);
    }
}
