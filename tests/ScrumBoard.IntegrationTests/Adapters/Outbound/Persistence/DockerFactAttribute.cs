namespace ScrumBoard.IntegrationTests.Adapters.Outbound.Persistence;

[AttributeUsage(AttributeTargets.Method)]
public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");
        var defaultSocket = "/var/run/docker.sock";
        var desktopSocket = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".docker", "run", "docker.sock");

        if (OperatingSystem.IsLinux() && string.IsNullOrWhiteSpace(dockerHost) &&
            !File.Exists(defaultSocket) && !File.Exists(desktopSocket))
        {
            Skip = "Docker is unavailable; PostgreSQL Testcontainers tests require a Docker socket.";
        }
    }
}
