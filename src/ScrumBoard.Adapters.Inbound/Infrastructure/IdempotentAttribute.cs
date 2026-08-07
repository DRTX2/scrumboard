namespace ScrumBoard.Adapters.Inbound.Infrastructure;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class IdempotentAttribute : Attribute;
