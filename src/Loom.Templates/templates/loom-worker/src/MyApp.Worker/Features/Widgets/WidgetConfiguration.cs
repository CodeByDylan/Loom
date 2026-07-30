using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyApp.Domain.Widgets;

namespace MyApp.Worker.Features.Widgets;

/// <summary>
/// How a widget is stored.
/// </summary>
/// <remarks>
/// Host-side and beside the aggregate's slices, so the mapping lives where the aggregate is worked on.
/// The domain stays free of persistence: it carries no attributes and no reference to Entity Framework.
/// </remarks>
internal sealed class WidgetConfiguration : IEntityTypeConfiguration<Widget>
{
    public void Configure(EntityTypeBuilder<Widget> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(widget => widget.Id);
        builder.Property(widget => widget.Name).HasMaxLength(200).IsRequired();
    }
}
