namespace Loom.Results.Tests;

public sealed class ResultTests
{
    private static Error AnError => Errors.Conflict("orders.already_cancelled", "The order is already cancelled.");

    [Test]
    public async Task Success_Reports_Success()
    {
        Result result = Result.Success;

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.IsFailure).IsFalse();
    }

    [Test]
    public async Task Failure_Carries_The_Error()
    {
        Result result = Result.Failure(AnError);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Category).IsEqualTo(ErrorCategory.Conflict);
    }

    [Test]
    public async Task An_Error_Converts_Implicitly_To_Failure()
    {
        Result result = AnError;

        await Assert.That(result.IsFailure).IsTrue();
    }

    [Test]
    public async Task Match_Invokes_The_Branch_Matching_The_Outcome()
    {
        await Assert.That(Result.Success.Match(() => "ok", _ => "bad")).IsEqualTo("ok");
        await Assert.That(Result.Failure(AnError).Match(() => "ok", _ => "bad")).IsEqualTo("bad");
    }

    [Test]
    public async Task Equal_Results_Compare_Equal()
    {
        Error error = AnError;

        await Assert.That(Result.Success == Result.Success).IsTrue();
        await Assert.That(Result.Failure(error) == Result.Failure(error)).IsTrue();
        await Assert.That(Result.Success != Result.Failure(error)).IsTrue();
    }

    [Test]
    public async Task Errors_With_Dictionary_Metadata_Compare_By_Reference()
    {
        // The record default, pinned because it surprises: identical inputs, distinct dictionaries,
        // unequal errors. The documented contract is discriminating on Code, not whole-error equality
        // — this test is the doc's claim made checkable.
        ValidationError first = new("orders.invalid", "Invalid.", new Dictionary<string, string[]>
        {
            ["sku"] = ["Unknown."],
        });
        ValidationError second = new("orders.invalid", "Invalid.", new Dictionary<string, string[]>
        {
            ["sku"] = ["Unknown."],
        });

        await Assert.That(first == second).IsFalse();
        await Assert.That(first.Code).IsEqualTo(second.Code);
    }

    [Test]
    public async Task ToString_Describes_The_Outcome()
    {
        await Assert.That(Result.Success.ToString()).IsEqualTo("Success");
        await Assert.That(Result.Failure(AnError).ToString()).IsEqualTo("Failure: orders.already_cancelled");
        await Assert.That(default(Result).ToString()).IsEqualTo("Uninitialized");
    }

    [Test]
    public async Task An_Uninitialized_Result_Throws_Rather_Than_Claiming_Failure()
    {
        Result uninitialized = default;

        await Assert.That(() => _ = uninitialized.IsSuccess).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Reading_The_Error_Of_A_Success_Throws()
    {
        await Assert.That(() => _ = Result.Success.Error).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task A_Failure_Cannot_Carry_A_Null_Error()
    {
        await Assert.That(() => Result.Failure(null!)).Throws<ArgumentNullException>();
    }
}
