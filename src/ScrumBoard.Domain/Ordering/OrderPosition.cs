using ScrumBoard.Domain.Primitives;

namespace ScrumBoard.Domain.Ordering;

public static class OrderPosition
{
    public const long Step = 1_024;

    public static long Between(long? previous, long? next)
    {
        if (previous is null && next is null)
        {
            return Step;
        }

        if (previous is null)
        {
            if (next <= 1)
            {
                throw RebalanceRequired();
            }

            return next.GetValueOrDefault() / 2;
        }

        if (next is null)
        {
            if (previous > long.MaxValue - Step)
            {
                throw RebalanceRequired();
            }

            return previous.GetValueOrDefault() + Step;
        }

        if (previous >= next || next - previous <= 1)
        {
            throw RebalanceRequired();
        }

        var previousValue = previous.GetValueOrDefault();
        var nextValue = next.GetValueOrDefault();
        return previousValue + ((nextValue - previousValue) / 2);
    }

    public static IReadOnlyList<long> Rebalance(int count) =>
        count >= 0
            ? Enumerable.Range(1, count).Select(index => checked((long)index * Step)).ToArray()
            : throw new DomainException("invalid_order_count", "La cantidad de elementos no puede ser negativa.");

    private static DomainException RebalanceRequired() =>
        new("order_rebalance_required", "La colección ordenada debe reequilibrarse antes de insertar el elemento.");
}
