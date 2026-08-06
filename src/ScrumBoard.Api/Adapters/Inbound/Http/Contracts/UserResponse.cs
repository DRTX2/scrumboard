using ScrumBoard.Domain.Projects;

namespace ScrumBoard.Api.Adapters.Inbound.Http.Contracts;

public sealed record UserResponse(Guid Id, string Name, ProjectRole Role);
