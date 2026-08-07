using System.ComponentModel.DataAnnotations;
using ScrumBoard.Domain.Tasks;

namespace ScrumBoard.Adapters.Inbound.Http.Contracts;

public sealed record TaskPageQueryRequest(
    [Range(1, 50, ErrorMessage = "El límite de tareas debe estar entre 1 y 50.")] int Limit = 20,
    [Range(1, long.MaxValue, ErrorMessage = "La posición del cursor debe ser mayor que cero.")] long? AfterPosition = null,
    Guid? AfterTaskId = null,
    Guid? AssigneeId = null,
    TaskPriority? Priority = null,
    [StringLength(200, ErrorMessage = "La búsqueda no puede superar 200 caracteres.")] string? Search = null)
    : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AfterPosition.HasValue != AfterTaskId.HasValue)
        {
            yield return new ValidationResult("La posición y el identificador del cursor deben enviarse juntos.");
        }

        if (AfterTaskId == Guid.Empty) yield return new ValidationResult("El identificador del cursor no es válido.", [nameof(AfterTaskId)]);
        if (AssigneeId == Guid.Empty) yield return new ValidationResult("La persona asignada no es válida.", [nameof(AssigneeId)]);
        if (Priority is not null && !Enum.IsDefined(Priority.Value))
        {
            yield return new ValidationResult("La prioridad no es válida.", [nameof(Priority)]);
        }
    }
}
