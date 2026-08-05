namespace ScrumBoard.Api.Adapters.SignalR;

internal static class BoardGroups
{
    public static string For(Guid projectId) => $"board:{projectId:N}";
}
