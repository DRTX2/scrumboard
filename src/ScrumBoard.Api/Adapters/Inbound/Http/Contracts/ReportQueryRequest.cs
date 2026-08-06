using System.ComponentModel.DataAnnotations;
using ScrumBoard.Domain.Tasks;

namespace ScrumBoard.Api.Adapters.Inbound.Http.Contracts;

public sealed record ReportQueryRequest(
    [Required(ErrorMessage = "El formato es obligatorio.")]
    [StringLength(10, ErrorMessage = "El formato no puede superar 10 caracteres.")]
    string Format,
    Guid? AssigneeId = null,
    TaskPriority? Priority = null,
    [StringLength(200, ErrorMessage = "La búsqueda no puede superar 200 caracteres.")] string? Search = null)
    : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AssigneeId == Guid.Empty) yield return new ValidationResult("La persona asignada no es válida.", [nameof(AssigneeId)]);
        if (Priority is not null && !Enum.IsDefined(Priority.Value))
        {
            yield return new ValidationResult("La prioridad no es válida.", [nameof(Priority)]);
        }
    }
}
