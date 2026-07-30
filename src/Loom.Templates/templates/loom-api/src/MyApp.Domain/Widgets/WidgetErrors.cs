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

    /// <summary>No widget exists under that identifier.</summary>
    public static Error NotFound { get; } =
        Errors.NotFound("widgets.not_found", "No such widget.");
}
