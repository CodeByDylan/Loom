using Loom.Results;

namespace MyApp.Domain.Widgets;

/// <summary>
/// The failures a widget can report. Codes are stable and greppable; messages are free to be reworded.
/// </summary>
public static class WidgetErrors
{
    /// <summary>A widget was created without a name.</summary>
    public static Error NameRequired { get; } =
        Errors.Invalid("widgets.name_required", "A widget needs a name.");

    /// <summary>A widget was created with a size of zero or less.</summary>
    public static Error SizeMustBePositive { get; } =
        Errors.Invalid("widgets.size_must_be_positive", "A widget's size must be greater than zero.");

    /// <summary>The widget was already out of service, so retiring it again means nothing.</summary>
    public static Error AlreadyRetired { get; } =
        Errors.Conflict("widgets.already_retired", "The widget is already retired.");

    /// <summary>Storage was briefly unreachable. Retrying may succeed, so the caller should.</summary>
    public static Error StorageUnavailable { get; } =
        Errors.Unavailable("widgets.storage_unavailable", "Widget storage is temporarily unavailable.");
}
