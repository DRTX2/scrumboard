using System.ComponentModel.DataAnnotations;

namespace ScrumBoard.Api.Adapters.Inbound.Http.Contracts;

public sealed record MoveTaskRequest(Guid ColumnId, Guid? BeforeTaskId, Guid? AfterTaskId) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ColumnId == Guid.Empty) yield return new ValidationResult("La columna es obligatoria.", [nameof(ColumnId)]);
        if (BeforeTaskId == Guid.Empty || AfterTaskId == Guid.Empty)
        {
            yield return new ValidationResult("Los identificadores de vecinos no pueden estar vacíos.");
        }

        if (BeforeTaskId is not null && BeforeTaskId == AfterTaskId)
        {
            yield return new ValidationResult("Los vecinos anterior y posterior deben ser distintos.");
        }
    }
}
