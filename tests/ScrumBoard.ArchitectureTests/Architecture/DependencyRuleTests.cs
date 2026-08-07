using System.Reflection;
using System.Xml.Linq;
using NetArchTest.Rules;
using ScrumBoard.Adapters.Inbound;
using ScrumBoard.Adapters.Inbound.Http;
using ScrumBoard.Adapters.Inbound.SignalR;
using ScrumBoard.Adapters.Outbound.Persistence;
using ScrumBoard.Application.Models.Projects;
using ScrumBoard.Application.Ports.Inbound.Boards;
using ScrumBoard.Application.Ports.Out;
using ScrumBoard.Application.UseCases.Projects;
using ScrumBoard.Domain.Projects;

namespace ScrumBoard.ArchitectureTests.Architecture;

public sealed class DependencyRuleTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void Domain_DoesNotDependOnOuterLayers()
    {
        var result = Types.InAssembly(typeof(Project).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "ScrumBoard.Application",
                "ScrumBoard.Adapters.Inbound",
                "ScrumBoard.Adapters.Outbound",
                "ScrumBoard.Api")
            .GetResult();

        AssertRule(result);
        Assert.DoesNotContain(typeof(Project).Assembly.GetReferencedAssemblies(),
            reference => reference.Name?.StartsWith("Microsoft.", StringComparison.Ordinal) is true);
        Assert.Empty(SolutionReferences(typeof(Project).Assembly));
    }

    [Fact]
    public void Application_DoesNotDependOnAdaptersOrApi()
    {
        var result = Types.InAssembly(typeof(ProjectUseCase).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "ScrumBoard.Adapters.Inbound",
                "ScrumBoard.Adapters.Outbound",
                "ScrumBoard.Api")
            .GetResult();

        AssertRule(result);
        Assert.DoesNotContain(typeof(ProjectUseCase).Assembly.GetReferencedAssemblies(),
            reference => reference.Name?.StartsWith("Microsoft.", StringComparison.Ordinal) is true);
        Assert.Equal(["ScrumBoard.Domain"], SolutionReferences(typeof(ProjectUseCase).Assembly));
    }

    [Fact]
    public void OutboundAdapters_DoNotDependOnInboundAdaptersOrApi()
    {
        var result = Types.InAssembly(typeof(ScrumBoardDbContext).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("ScrumBoard.Adapters.Inbound", "ScrumBoard.Api")
            .GetResult();

        AssertRule(result);
        Assert.Equal(
            ["ScrumBoard.Application", "ScrumBoard.Domain"],
            SolutionReferences(typeof(ScrumBoardDbContext).Assembly));
    }

    [Fact]
    public void InboundAdapters_DoNotDependOnOutboundAdaptersOrApi()
    {
        var result = Types.InAssembly(typeof(InboundAdapterExtensions).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("ScrumBoard.Adapters.Outbound", "ScrumBoard.Api")
            .GetResult();

        AssertRule(result);
        Assert.Equal(
            ["ScrumBoard.Application", "ScrumBoard.Domain"],
            SolutionReferences(typeof(InboundAdapterExtensions).Assembly));
    }

    [Fact]
    public void HttpControllers_DoNotDependOnOutboundAdapters()
    {
        var result = Types.InAssembly(typeof(BoardsController).Assembly)
            .That()
            .ResideInNamespaceStartingWith("ScrumBoard.Adapters.Inbound.Http")
            .ShouldNot()
            .HaveDependencyOnAny("ScrumBoard.Adapters.Outbound", "Microsoft.EntityFrameworkCore")
            .GetResult();

        AssertRule(result);
    }

    [Fact]
    public void InboundInfrastructure_DoesNotDependOnOutboundAdapters()
    {
        var result = Types.InAssembly(typeof(BoardsController).Assembly)
            .That()
            .ResideInNamespaceStartingWith("ScrumBoard.Adapters.Inbound.Infrastructure")
            .ShouldNot()
            .HaveDependencyOnAny(
                "ScrumBoard.Adapters.Outbound",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        AssertRule(result);
    }

    [Fact]
    public void InboundPorts_DoNotDependOnUseCasesOrAdapters()
    {
        var result = Types.InAssembly(typeof(IBoardUseCase).Assembly)
            .That()
            .ResideInNamespaceStartingWith("ScrumBoard.Application.Ports.Inbound")
            .ShouldNot()
            .HaveDependencyOnAny(
                "ScrumBoard.Application.UseCases",
                "ScrumBoard.Adapters.Inbound",
                "ScrumBoard.Adapters.Outbound",
                "ScrumBoard.Api")
            .GetResult();

        AssertRule(result);
    }

    [Fact]
    public void OutboundPorts_DoNotDependOnInboundPortsUseCasesOrAdapters()
    {
        var result = Types.InAssembly(typeof(IBoardRepository).Assembly)
            .That()
            .ResideInNamespaceStartingWith("ScrumBoard.Application.Ports.Out")
            .ShouldNot()
            .HaveDependencyOnAny(
                "ScrumBoard.Application.Ports.Inbound",
                "ScrumBoard.Application.UseCases",
                "ScrumBoard.Adapters.Inbound",
                "ScrumBoard.Adapters.Outbound",
                "ScrumBoard.Api")
            .GetResult();

        AssertRule(result);
        var objectParameters = typeof(IBoardRepository).Assembly.GetTypes()
            .Where(type => type.IsInterface && type.Namespace == "ScrumBoard.Application.Ports.Out")
            .SelectMany(type => type.GetMethods())
            .SelectMany(method => method.GetParameters())
            .Where(parameter => parameter.ParameterType == typeof(object))
            .ToList();
        Assert.Empty(objectParameters);
    }

    [Fact]
    public void ApplicationModels_DoNotDependOnPortsUseCasesOrAdapters()
    {
        var result = Types.InAssembly(typeof(ProjectSearchCriteria).Assembly)
            .That()
            .ResideInNamespaceStartingWith("ScrumBoard.Application.Models")
            .ShouldNot()
            .HaveDependencyOnAny(
                "ScrumBoard.Application.Ports",
                "ScrumBoard.Application.UseCases",
                "ScrumBoard.Adapters.Inbound",
                "ScrumBoard.Adapters.Outbound",
                "ScrumBoard.Api")
            .GetResult();

        AssertRule(result);
    }

    [Fact]
    public void OutboundAdapters_DoNotDependOnInboundPorts()
    {
        var result = Types.InAssembly(typeof(ScrumBoardDbContext).Assembly)
            .That()
            .ResideInNamespaceStartingWith("ScrumBoard.Adapters.Outbound")
            .ShouldNot()
            .HaveDependencyOn("ScrumBoard.Application.Ports.Inbound")
            .GetResult();

        AssertRule(result);
    }

    [Fact]
    public void RealtimeAdapter_UsesOneProtocolNamespace()
    {
        var misplaced = typeof(BoardHub).Assembly.GetTypes()
            .Where(type => type.Namespace?.Contains("SignalR", StringComparison.Ordinal) is true)
            .Where(type => type.Namespace != "ScrumBoard.Adapters.Inbound.SignalR")
            .Select(type => type.FullName)
            .ToList();

        Assert.Empty(misplaced);
    }

    [Fact]
    public void Migrator_DependsOnlyOnOutboundAdaptersWithinTheSolution()
    {
        var assembly = Assembly.Load("ScrumBoard.Migrator");

        Assert.Equal(["ScrumBoard.Adapters.Outbound"], SolutionReferences(assembly));
    }

    [Fact]
    public void Api_ContainsOnlyHostConfigurationAndCompositionTypes()
    {
        var misplaced = typeof(Program).Assembly.GetTypes()
            .Where(type => type != typeof(Program) && type.Namespace is { } name &&
                !name.StartsWith("Coverlet.Core.Instrumentation.Tracker", StringComparison.Ordinal) &&
                !name.StartsWith("ScrumBoard.Api.Configuration", StringComparison.Ordinal) &&
                !name.StartsWith("ScrumBoard.Api.Composition", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToList();

        Assert.Empty(misplaced);
        Assert.Equal(
            ["ScrumBoard.Adapters.Inbound", "ScrumBoard.Adapters.Outbound", "ScrumBoard.Application"],
            SolutionReferences(typeof(Program).Assembly));
    }

    [Fact]
    public void ProductionProjects_HaveExactInwardProjectReferences()
    {
        var expectedReferences = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["src/ScrumBoard.Domain/ScrumBoard.Domain.csproj"] = [],
            ["src/ScrumBoard.Application/ScrumBoard.Application.csproj"] = ["ScrumBoard.Domain"],
            ["src/ScrumBoard.Adapters.Inbound/ScrumBoard.Adapters.Inbound.csproj"] =
                ["ScrumBoard.Application", "ScrumBoard.Domain"],
            ["src/ScrumBoard.Adapters.Outbound/ScrumBoard.Adapters.Outbound.csproj"] =
                ["ScrumBoard.Application", "ScrumBoard.Domain"],
            ["src/ScrumBoard.Api/ScrumBoard.Api.csproj"] =
                ["ScrumBoard.Adapters.Inbound", "ScrumBoard.Adapters.Outbound", "ScrumBoard.Application"],
            ["src/ScrumBoard.Migrator/ScrumBoard.Migrator.csproj"] = ["ScrumBoard.Adapters.Outbound"]
        };

        foreach (var (relativePath, expected) in expectedReferences)
        {
            Assert.Equal(expected, ProjectReferences(relativePath));
        }
    }

    [Theory]
    [InlineData("src/ScrumBoard.Domain/ScrumBoard.Domain.csproj")]
    [InlineData("src/ScrumBoard.Application/ScrumBoard.Application.csproj")]
    public void CoreProjects_HaveNoFrameworkOrPackageReferences(string relativePath)
    {
        var project = XDocument.Load(Path.Combine(RepositoryRoot, relativePath));
        var externalReferences = project.Descendants()
            .Where(element => element.Name.LocalName is "PackageReference" or "FrameworkReference")
            .Select(element => element.Attribute("Include")?.Value)
            .OfType<string>()
            .ToList();

        Assert.Empty(externalReferences);
    }

    private static string[] SolutionReferences(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .OfType<string>()
            .Where(name => name.StartsWith("ScrumBoard.", StringComparison.Ordinal))
            .Order()
            .ToArray();

    private static string[] ProjectReferences(string relativePath)
    {
        var projectPath = Path.Combine(RepositoryRoot, relativePath);
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        return XDocument.Load(projectPath).Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .OfType<string>()
            .Select(reference => reference.Replace('\\', Path.DirectorySeparatorChar))
            .Select(reference => Path.GetFullPath(reference, projectDirectory))
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .Order()
            .ToArray();
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ScrumBoard.sln"))) return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }

    private static void AssertRule(TestResult result) =>
        Assert.True(result.IsSuccessful, $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
}
