using System.ComponentModel.DataAnnotations;

namespace ScrumBoard.Api.Adapters.Inbound.Http.Contracts;

public sealed record CreateSessionRequest(
    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [StringLength(254, ErrorMessage = "El correo electrónico no puede superar 254 caracteres.")]
    [EmailAddress(ErrorMessage = "El correo electrónico no tiene un formato válido.")]
    string Email,
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(256, ErrorMessage = "La contraseña no puede superar 256 caracteres.")]
    string Password);
