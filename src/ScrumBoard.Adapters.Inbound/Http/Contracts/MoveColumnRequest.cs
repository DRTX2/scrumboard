using System.ComponentModel.DataAnnotations;

namespace ScrumBoard.Adapters.Inbound.Http.Contracts;

public sealed record MoveColumnRequest(Guid? BeforeColumnId, Guid? AfterColumnId) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (BeforeColumnId == Guid.Empty || AfterColumnId == Guid.Empty)
        {
            yield return new ValidationResult("Los identificadores de vecinos no pueden estar vacíos.");
        }

        if (BeforeColumnId is not null && BeforeColumnId == AfterColumnId)
        {
            yield return new ValidationResult("Los vecinos anterior y posterior deben ser distintos.");
        }
    }
}
