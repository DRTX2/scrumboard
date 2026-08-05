namespace ScrumBoard.Api.Infrastructure;

internal sealed class PostCommitActionQueue
{
    private List<Func<CancellationToken, Task>>? _actions;

    public void BeginDeferral() => _actions = [];

    public bool TryEnqueue(Func<CancellationToken, Task> action)
    {
        if (_actions is null) return false;
        _actions.Add(action);
        return true;
    }

    public async Task DrainAsync(CancellationToken cancellationToken)
    {
        var actions = _actions ?? [];
        _actions = null;
        foreach (var action in actions) await action(cancellationToken);
    }

    public void Discard() => _actions = null;
}
