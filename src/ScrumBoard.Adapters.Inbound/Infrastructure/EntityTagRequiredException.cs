namespace ScrumBoard.Adapters.Inbound.Infrastructure;

internal sealed class EntityTagRequiredException()
    : Exception("La cabecera If-Match es obligatoria para esta operación.");
