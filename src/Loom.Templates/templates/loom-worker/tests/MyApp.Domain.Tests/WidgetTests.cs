using Loom.Results;
using MyApp.Domain.Widgets;

namespace MyApp.Domain.Tests;

/// <summary>
/// Pure, fast, no infrastructure. Invariants are testable without a database because they live in the
/// domain rather than in a handler.
/// </summary>
public sealed class WidgetTests
{
    [Test]
    public async Task A_Valid_Widget_Is_Created()
    {
        Result<Widget> created = Widget.Create("bolt", 3);

        await Assert.That(created.IsSuccess).IsTrue();
        await Assert.That(created.Value.Name).IsEqualTo("bolt");
    }

    [Test]
    public async Task A_Widget_Without_A_Name_Is_Refused()
    {
        Result<Widget> created = Widget.Create("  ", 3);

        await Assert.That(created.IsFailure).IsTrue();
        await Assert.That(created.Error.Code).IsEqualTo(WidgetErrors.NameRequired.Code);
    }

    [Test]
    public async Task A_Widget_Must_Have_A_Positive_Size()
    {
        Result<Widget> created = Widget.Create("bolt", 0);

        await Assert.That(created.IsFailure).IsTrue();
        await Assert.That(created.Error.Code).IsEqualTo(WidgetErrors.SizeMustBePositive.Code);
    }

    [Test]
    public async Task A_Widget_Can_Be_Retired_Once()
    {
        Widget widget = Widget.Create("bolt", 3).Value;

        await Assert.That(widget.Retire().IsSuccess).IsTrue();
        await Assert.That(widget.IsRetired).IsTrue();
    }

    [Test]
    public async Task Retiring_A_Retired_Widget_Reports_A_Conflict()
    {
        // The invariant the slice leans on: a widget retired by something else between the query and
        // the loop reports Conflict, which the pass counts as information rather than failure.
        Widget widget = Widget.Create("bolt", 3).Value;
        _ = widget.Retire();

        Result again = widget.Retire();

        await Assert.That(again.IsFailure).IsTrue();
        await Assert.That(again.Error.Code).IsEqualTo(WidgetErrors.AlreadyRetired.Code);
        await Assert.That(again.Error.Category).IsEqualTo(ErrorCategory.Conflict);
    }
}
