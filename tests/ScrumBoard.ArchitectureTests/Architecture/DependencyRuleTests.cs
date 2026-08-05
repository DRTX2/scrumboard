using System.Reflection;
using NetArchTest.Rules;
using ScrumBoard.Application.Models.Projects;
using ScrumBoard.Application.Ports.Inbound.Boards;
using ScrumBoard.Application.Ports.Outbound;
using ScrumBoard.Application.UseCases.Projects;
using ScrumBoard.Domain.Projects;
using ScrumBoard.Infrastructure.Adapters.Outbound.Persistence;

namespace ScrumBoard.ArchitectureTests.Architecture;

public sealed class DependencyRuleTests
{
    [Fact]
    public void Domain_DoesNotDependOnOuterLayers()
    {
        var result = Types.InAssembly(typeof(Project).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("ScrumBoard.Application", "ScrumBoard.Infrastructure", "ScrumBoard.Api")
            .GetResult();

        AssertRule(result);
        Assert.DoesNotContain(typeof(Project).Assembly.GetReferencedAssemblies(),
            reference => reference.Name?.StartsWith("Microsoft.", StringComparison.Ordinal) is true);
        Assert.Empty(SolutionReferences(typeof(Project).Assembly));
    }

    [Fact]
    public void Application_DoesNotDependOnInfrastructureOrApi()
    {
        var result = Types.InAssembly(typeof(ProjectUseCase).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("ScrumBoard.Infrastructure", "ScrumBoard.Api")
            .GetResult();

        AssertRule(result);
        Assert.DoesNotContain(typeof(ProjectUseCase).Assembly.GetReferencedAssemblies(),
            reference => reference.Name?.StartsWith("Microsoft.", StringComparison.Ordinal) is true);
        Assert.Equal(["ScrumBoard.Domain"], SolutionReferences(typeof(ProjectUseCase).Assembly));
    }

    [Fact]
    public void Infrastructure_DoesNotDependOnApi()
    {
        var result = Types.InAssembly(typeof(ScrumBoardDbContext).Assembly)
            .ShouldNot()
            .HaveDependencyOn("ScrumBoard.Api")
            .GetResult();

        AssertRule(result);
    }

    [Fact]
    public void ApiControllers_DoNotDependDirectlyOnInfrastructure()
    {
        var result = Types.InAssembly(typeof(Program).Assembly)
            .That()
            .ResideInNamespaceStartingWith("ScrumBoard.Api.Adapters.Inbound.Http")
            .ShouldNot()
            .HaveDependencyOn("ScrumBoard.Infrastructure")
            .GetResult();

        AssertRule(result);
    }

    [Fact]
    public void ApiInfrastructure_DoesNotDependDirectlyOnInfrastructureAdapters()
    {
        var result = Types.InAssembly(typeof(Program).Assembly)
            .That()
            .ResideInNamespaceStartingWith("ScrumBoard.Api.Infrastructure")
            .ShouldNot()
            .HaveDependencyOnAny(
                "ScrumBoard.Application.Ports.Outbound",
                "ScrumBoard.Infrastructure",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        AssertRule(result);
    }

    [Fact]
    public void InboundPorts_DoNotDependOnUseCasesOrInfrastructure()
    {
        var result = Types.InAssembly(typeof(IBoardUseCase).Assembly)
            .That()
            .ResideInNamespaceStartingWith("ScrumBoard.Application.Ports.Inbound")
            .ShouldNot()
            .HaveDependencyOnAny("ScrumBoard.Application.UseCases", "ScrumBoard.Infrastructure", "ScrumBoard.Api")
            .GetResult();

        AssertRule(result);
    }

    [Fact]
    public void OutboundPorts_DoNotDependOnInboundPortsUseCasesOrAdapters()
    {
        var result = Types.InAssembly(typeof(IBoardRepository).Assembly)
            .That()
            .ResideInNamespaceStartingWith("ScrumBoard.Application.Ports.Outbound")
            .ShouldNot()
            .HaveDependencyOnAny(
                "ScrumBoard.Application.Ports.Inbound",
                "ScrumBoard.Application.UseCases",
                "ScrumBoard.Infrastructure",
                "ScrumBoard.Api")
            .GetResult();

        AssertRule(result);
        var objectParameters = typeof(IBoardRepository).Assembly.GetTypes()
            .Where(type => type.IsInterface && type.Namespace == "ScrumBoard.Application.Ports.Outbound")
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
                "ScrumBoard.Infrastructure",
                "ScrumBoard.Api")
            .GetResult();

        AssertRule(result);
    }

    [Fact]
    public void InfrastructureAdapters_DoNotDependOnInboundPorts()
    {
        var result = Types.InAssembly(typeof(ScrumBoardDbContext).Assembly)
            .That()
            .ResideInNamespaceStartingWith("ScrumBoard.Infrastructure.Adapters.Outbound")
            .ShouldNot()
            .HaveDependencyOn("ScrumBoard.Application.Ports.Inbound")
            .GetResult();

        AssertRule(result);
    }

    [Fact]
    public void SignalRTypes_UseOneTechnicalAdapterNamespace()
    {
        var misplaced = typeof(Program).Assembly.GetTypes()
            .Where(type => type.Namespace?.Contains("SignalR", StringComparison.Ordinal) is true)
            .Where(type => type.Namespace != "ScrumBoard.Api.Adapters.SignalR")
            .Select(type => type.FullName)
            .ToList();

        Assert.Empty(misplaced);
    }

    [Fact]
    public void Migrator_DependsOnlyOnInfrastructureWithinTheSolution()
    {
        var assembly = Assembly.Load("ScrumBoard.Migrator");

        Assert.Equal(["ScrumBoard.Infrastructure"], SolutionReferences(assembly));
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
