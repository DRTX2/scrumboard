using System.ComponentModel.DataAnnotations;

namespace ScrumBoard.Api.Adapters.Inbound.Http.Contracts;

public sealed record CreateColumnRequest(
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar 100 caracteres.")]
    string Name);
