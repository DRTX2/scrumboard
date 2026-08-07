using System.ComponentModel.DataAnnotations;

namespace ScrumBoard.Adapters.Inbound.Http.Contracts;

public sealed record UpdateColumnRequest(
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar 100 caracteres.")]
    string Name);
