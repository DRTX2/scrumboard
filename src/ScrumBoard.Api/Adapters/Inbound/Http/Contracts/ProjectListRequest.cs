using System.ComponentModel.DataAnnotations;

namespace ScrumBoard.Api.Adapters.Inbound.Http.Contracts;

public sealed record ProjectListRequest(
    [Range(1, 100_000, ErrorMessage = "La página debe estar entre 1 y 100000.")] int Page = 1,
    [Range(1, 100, ErrorMessage = "El tamaño de página debe estar entre 1 y 100.")] int PageSize = 20,
    [StringLength(200, ErrorMessage = "La búsqueda no puede superar 200 caracteres.")] string? Search = null,
    [RegularExpression("(?i)^(updatedAt|name|startDate|status)$", ErrorMessage = "El campo de ordenación no es válido.")]
    string Sort = "updatedAt",
    [RegularExpression("(?i)^(asc|desc)$", ErrorMessage = "La dirección de ordenación no es válida.")]
    string Direction = "desc");
