namespace Loom.Persistence;

/// <summary>
/// Configures deferred domain event delivery.
/// </summary>
public sealed class OutboxOptions
{
    private int _batchSize = 50;
    private int _maximumAttempts = 5;
    private TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets how many messages one pass delivers.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than one.</exception>
    public int BatchSize
    {
        get => _batchSize;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _batchSize = value;
        }
    }

    /// <summary>
    /// Gets or sets how many times delivery is attempted before a message is abandoned.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than one.</exception>
    public int MaximumAttempts
    {
        get => _maximumAttempts;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _maximumAttempts = value;
        }
    }

    /// <summary>
    /// Gets or sets how long to wait between passes when there is nothing to deliver.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public TimeSpan PollingInterval
    {
        get => _pollingInterval;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            _pollingInterval = value;
        }
    }

    /// <summary>
    /// Gets or sets the assemblies searched when reconstructing an event from its recorded type name.
    /// </summary>
    /// <remarks>
    /// Names are stored without assembly version, so that a version bump does not orphan messages
    /// already recorded. Naming the assemblies to search keeps resolution cheap and unambiguous.
    /// </remarks>
    public IReadOnlyList<System.Reflection.Assembly> EventAssemblies { get; set; } = [];
}
