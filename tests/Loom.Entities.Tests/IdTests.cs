using System.Text.Json;

namespace Loom.Entities.Tests;

public class IdTests
{
    [Test]
    public async Task New_Produces_A_Version_7_Value()
    {
        Id<Order> id = Id<Order>.New();

        // Version 7 is time-ordered, which is what makes CompareTo meaningful and keeps index
        // locality good on insert.
        await Assert.That(id.Value.Version).IsEqualTo(7);
    }

    [Test]
    public async Task New_Produces_Distinct_Values()
    {
        await Assert.That(Id<Order>.New()).IsNotEqualTo(Id<Order>.New());
    }

    [Test]
    public async Task Identities_With_The_Same_Value_Are_Equal()
    {
        Guid value = Guid.CreateVersion7();

        await Assert.That(Id<Order>.From(value)).IsEqualTo(Id<Order>.From(value));
        await Assert.That(Id<Order>.From(value) == Id<Order>.From(value)).IsTrue();
    }

    [Test]
    public async Task From_Rejects_An_Empty_Value()
    {
        await Assert.That(() => Id<Order>.From(Guid.Empty)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Identities_Created_Milliseconds_Apart_Sort_By_Creation_Time()
    {
        Id<Order> first = Id<Order>.New();
        await Task.Delay(millisecondsDelay: 3);
        Id<Order> second = Id<Order>.New();

        // Only holds across a millisecond boundary. Version 7 embeds a millisecond timestamp and
        // .NET does not make values monotonic within one, so two identities created back to back
        // sort arbitrarily — measured at roughly a 50% inversion rate. Ordering is good enough for
        // index locality, and must not be used as a creation-order key.
        await Assert.That(first < second).IsTrue();
        await Assert.That(second > first).IsTrue();
    }

    [Test]
    public async Task Comparison_Is_A_Consistent_Total_Order()
    {
        Id<Order>[] ids = [Id<Order>.New(), Id<Order>.New(), Id<Order>.New()];

        Id<Order>[] sorted = [.. ids.Order()];
        Id<Order>[] sortedAgain = [.. ids.Reverse().Order()];

        await Assert.That(sorted).IsEquivalentTo(sortedAgain);
    }

    [Test]
    public async Task Parse_Round_Trips_ToString()
    {
        Id<Order> id = Id<Order>.New();

        await Assert.That(Id<Order>.Parse(id.ToString(), provider: null)).IsEqualTo(id);
    }

    [Test]
    public async Task TryParse_Rejects_Nonsense_And_Empty()
    {
        await Assert.That(Id<Order>.TryParse("not-an-id", provider: null, out _)).IsFalse();
        await Assert.That(Id<Order>.TryParse(Guid.Empty.ToString(), provider: null, out _)).IsFalse();
        await Assert.That(Id<Order>.TryParse(null, provider: null, out _)).IsFalse();
    }

    [Test]
    public async Task An_Identity_Serializes_As_A_Bare_String()
    {
        Id<Order> id = Id<Order>.New();

        string json = JsonSerializer.Serialize(id);

        // Not {"value":"..."} — every client expects a string here.
        await Assert.That(json).IsEqualTo($"\"{id.Value}\"");
    }

    [Test]
    public async Task An_Identity_Round_Trips_Through_Json()
    {
        Id<Order> id = Id<Order>.New();

        string json = JsonSerializer.Serialize(id);
        Id<Order> restored = JsonSerializer.Deserialize<Id<Order>>(json);

        await Assert.That(restored).IsEqualTo(id);
    }

    [Test]
    public async Task An_Identity_Nested_In_An_Object_Round_Trips()
    {
        Payload payload = new(Id<Order>.New(), "loom");

        Payload? restored = JsonSerializer.Deserialize<Payload>(JsonSerializer.Serialize(payload));

        await Assert.That(restored!.OrderId).IsEqualTo(payload.OrderId);
    }

    [Test]
    public async Task Invalid_Json_Is_Rejected()
    {
        await Assert.That(() => JsonSerializer.Deserialize<Id<Order>>("\"nonsense\""))
            .Throws<JsonException>();
    }

    [Test]
    public async Task Unwrapping_Requires_An_Explicit_Cast()
    {
        Id<Order> id = Id<Order>.New();

        await Assert.That((Guid)id).IsEqualTo(id.Value);
    }

    internal sealed record Payload(Id<Order> OrderId, string Name);
}
