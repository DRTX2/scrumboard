using NetArchTest.Rules;
using ScrumBoard.Application.Projects;
using ScrumBoard.Domain.Projects;
using ScrumBoard.Infrastructure.Persistence;

namespace ScrumBoard.ArchitectureTests;

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
    }

    [Fact]
    public void Application_DoesNotDependOnInfrastructureOrApi()
    {
        var result = Types.InAssembly(typeof(ProjectService).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("ScrumBoard.Infrastructure", "ScrumBoard.Api")
            .GetResult();

        AssertRule(result);
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
            .ResideInNamespace("ScrumBoard.Api.Controllers")
            .ShouldNot()
            .HaveDependencyOn("ScrumBoard.Infrastructure")
            .GetResult();

        AssertRule(result);
    }

    private static void AssertRule(TestResult result) =>
        Assert.True(result.IsSuccessful, $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
}
