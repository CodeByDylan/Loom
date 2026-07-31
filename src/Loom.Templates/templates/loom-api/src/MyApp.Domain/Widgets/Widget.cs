using Loom.Entities;
using Loom.Results;

namespace MyApp.Domain.Widgets;

/// <summary>
/// An example aggregate. Replace it — it exists to show the shape, not to be kept.
/// </summary>
public sealed class Widget : AggregateRoot<Widget>
{
    private Widget(string name, int size)
    {
        Name = name;
        Size = size;
    }

    // Entity Framework materialises through this. It writes the identity from the column, so a
    // parameterless constructor stays unambiguous whatever the domain constructors look like.
    private Widget()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public int Size { get; private set; }

    /// <summary>
    /// Creates a widget, or reports why it could not be created.
    /// </summary>
    /// <remarks>
    /// A factory returning a result rather than a constructor throwing: an invalid request is an
    /// expected outcome, and expected outcomes are values. The invariant lives here, not in a
    /// validator — a validator checks the shape of a request, not whether the domain permits it.
    /// </remarks>
    public static Result<Widget> Create(string name, int size)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return WidgetErrors.NameRequired;
        }

        if (size <= 0)
        {
            return WidgetErrors.SizeMustBePositive;
        }

        return new Widget(name, size);
    }
}
