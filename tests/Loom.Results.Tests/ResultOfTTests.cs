namespace Loom.Results.Tests;

public class ResultOfTTests
{
    private static Error AnError => Errors.NotFound("orders.not_found", "No such order.");

    [Test]
    public async Task Success_Carries_The_Value()
    {
        Result<int> result = Result<int>.Success(42);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.IsFailure).IsFalse();
        await Assert.That(result.Value).IsEqualTo(42);
    }

    [Test]
    public async Task Failure_Carries_The_Error()
    {
        Result<int> result = Result<int>.Failure(AnError);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Category).IsEqualTo(ErrorCategory.NotFound);
        await Assert.That(result.Error.Code).IsEqualTo("orders.not_found");
    }

    [Test]
    public async Task A_Value_Converts_Implicitly_To_Success()
    {
        Result<string> result = "loom";

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value).IsEqualTo("loom");
    }

    [Test]
    public async Task An_Error_Converts_Implicitly_To_Failure()
    {
        Result<string> result = AnError;

        await Assert.That(result.IsFailure).IsTrue();
    }

    [Test]
    public async Task A_Derived_Error_Converts_Implicitly_To_Failure()
    {
        // The base-class conversion must apply to consumer-defined error types, which is the whole
        // reason Error is a class rather than an interface.
        Result<string> result = new ValidationError(
            "orders.invalid",
            "The request is invalid.",
            new Dictionary<string, string[]> { ["Sku"] = ["Required."] });

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Category).IsEqualTo(ErrorCategory.Invalid);
    }

    [Test]
    public async Task TryGetValue_Yields_The_Value_On_Success()
    {
        Result<string> result = Result<string>.Success("loom");

        bool found = result.TryGetValue(out string? value);

        await Assert.That(found).IsTrue();
        await Assert.That(value).IsEqualTo("loom");
    }

    [Test]
    public async Task TryGetValue_Yields_Nothing_On_Failure()
    {
        Result<string> result = AnError;

        bool found = result.TryGetValue(out string? value);

        await Assert.That(found).IsFalse();
        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task Match_Invokes_The_Branch_Matching_The_Outcome()
    {
        Result<int> success = 10;
        Result<int> failure = AnError;

        await Assert.That(success.Match(v => v * 2, _ => -1)).IsEqualTo(20);
        await Assert.That(failure.Match(v => v * 2, _ => -1)).IsEqualTo(-1);
    }

    [Test]
    public async Task Discarding_The_Value_Keeps_The_Outcome()
    {
        Result success = Result<int>.Success(1);
        Result failure = Result<int>.Failure(AnError);

        await Assert.That(success.IsSuccess).IsTrue();
        await Assert.That(failure.IsFailure).IsTrue();
        await Assert.That(failure.Error.Code).IsEqualTo("orders.not_found");
    }

    [Test]
    public async Task Equal_Results_Compare_Equal()
    {
        Error error = AnError;

        await Assert.That(Result<int>.Success(1)).IsEqualTo(Result<int>.Success(1));
        await Assert.That(Result<int>.Success(1) == Result<int>.Success(1)).IsTrue();
        await Assert.That(Result<int>.Success(1) != Result<int>.Success(2)).IsTrue();
        await Assert.That(Result<int>.Failure(error) == Result<int>.Failure(error)).IsTrue();
        await Assert.That(Result<int>.Success(1) != Result<int>.Failure(error)).IsTrue();
    }

    [Test]
    public async Task ToString_Describes_The_Outcome()
    {
        await Assert.That(Result<int>.Success(42).ToString()).IsEqualTo("Success: 42");
        await Assert.That(Result<int>.Failure(AnError).ToString()).IsEqualTo("Failure: orders.not_found");
        await Assert.That(default(Result<int>).ToString()).IsEqualTo("Uninitialized");
    }

    // The four guards below are the entire safety argument for the readonly struct design.

    [Test]
    public async Task An_Uninitialized_Result_Throws_Rather_Than_Claiming_Failure()
    {
        Result<int> uninitialized = default;

        await Assert.That(() => _ = uninitialized.IsSuccess).Throws<InvalidOperationException>();
        await Assert.That(() => _ = uninitialized.IsFailure).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Reading_The_Value_Of_A_Failure_Throws()
    {
        Result<int> result = AnError;

        await Assert.That(() => _ = result.Value).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Reading_The_Error_Of_A_Success_Throws()
    {
        Result<int> result = 1;

        await Assert.That(() => _ = result.Error).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task A_Successful_Result_Cannot_Carry_Null()
    {
        await Assert.That(() => Result<string>.Success(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task A_Failure_Cannot_Carry_A_Null_Error()
    {
        await Assert.That(() => Result<string>.Failure(null!)).Throws<ArgumentNullException>();
    }
}
