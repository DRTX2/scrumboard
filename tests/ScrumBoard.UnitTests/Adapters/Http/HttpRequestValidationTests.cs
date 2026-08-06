using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using ScrumBoard.Api.Adapters.Inbound.Http.Contracts;
using ScrumBoard.Api.Infrastructure;
using ScrumBoard.Application.Models.Projects;
using ScrumBoard.Domain.Projects;
using ScrumBoard.Domain.Tasks;

namespace ScrumBoard.UnitTests.Adapters.Http;

public sealed class HttpRequestValidationTests
{
    [Fact]
    public void CreateTask_WithoutAssignee_IsRejectedByHttpModelValidation()
    {
        var request = new CreateTaskRequest(
            Guid.NewGuid(), "Tarea", null, TaskPriority.Medium, Guid.Empty, null);

        var errors = Validate(request);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(CreateTaskRequest.AssigneeId)));
    }

    [Fact]
    public void Project_WithReversedDates_IsRejectedByHttpModelValidation()
    {
        var request = new CreateProjectRequest(
            "Proyecto",
            null,
            new DateOnly(2026, 8, 6),
            new DateOnly(2026, 8, 5),
            ProjectStatus.Active);

        var errors = Validate(request);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(CreateProjectRequest.ExpectedEndDate)));
    }

    [Fact]
    public void TaskPage_WithPartialCursor_IsRejectedByHttpModelValidation()
    {
        var request = new TaskPageQueryRequest(AfterPosition: 1024);

        Assert.NotEmpty(Validate(request));
    }

    [Fact]
    public void ProjectResponseMapping_KeepsAuthorizationRole()
    {
        var project = new ProjectSummary(
            Guid.NewGuid(),
            "Proyecto",
            null,
            new DateOnly(2026, 8, 5),
            new DateOnly(2026, 8, 6),
            ProjectStatus.Active,
            ProjectRole.Owner,
            1,
            DateTimeOffset.UtcNow);

        var response = project.ToResponse();

        Assert.Equal(ProjectRole.Owner, response.Role);
    }

    [Fact]
    public void EntityTags_Require_AcceptsSingleStrongNumericTag()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.IfMatch = "\"42\"";

        Assert.Equal(42, EntityTags.Require(context.Request));
    }

    [Theory]
    [InlineData("W/\"42\"")]
    [InlineData("42")]
    [InlineData("\"0\"")]
    [InlineData("\"42\", \"43\"")]
    [InlineData("\"not-a-number\"")]
    [InlineData("\"+42\"")]
    [InlineData("\" 42\"")]
    public void EntityTags_Require_RejectsWeakMalformedAndListValues(string value)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.IfMatch = value;

        Assert.Throws<BadHttpRequestException>(() => EntityTags.Require(context.Request));
    }

    [Fact]
    public void EntityTags_Require_RejectsMultipleHeaderValues()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Append("If-Match", "\"42\"");
        context.Request.Headers.Append("If-Match", "\"43\"");

        Assert.Throws<BadHttpRequestException>(() => EntityTags.Require(context.Request));
    }

    private static List<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true);
        return results;
    }
}
