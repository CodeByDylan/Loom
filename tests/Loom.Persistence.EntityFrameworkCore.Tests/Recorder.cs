namespace Loom.Persistence.EntityFrameworkCore.Tests;

/// <summary>Records what handlers did, through the container rather than static state.</summary>
internal sealed class Recorder
{
    private readonly List<string> _handled = [];

    internal IReadOnlyList<string> Handled
    {
        get
        {
            lock (_handled)
            {
                return [.. _handled];
            }
        }
    }

    internal void Record(string what)
    {
        lock (_handled)
        {
            _handled.Add(what);
        }
    }
}
