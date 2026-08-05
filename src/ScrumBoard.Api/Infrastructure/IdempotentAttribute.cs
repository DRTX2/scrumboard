namespace ScrumBoard.Api.Infrastructure;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class IdempotentAttribute : Attribute;
