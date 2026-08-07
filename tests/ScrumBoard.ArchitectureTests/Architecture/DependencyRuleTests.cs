using System.Reflection;
using NetArchTest.Rules;
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
    public void SignalRTypes_UseOneTechnicalAdapterNamespace()
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

    private static string[] SolutionReferences(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .OfType<string>()
            .Where(name => name.StartsWith("ScrumBoard.", StringComparison.Ordinal))
            .Order()
            .ToArray();

    private static void AssertRule(TestResult result) =>
        Assert.True(result.IsSuccessful, $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
}
