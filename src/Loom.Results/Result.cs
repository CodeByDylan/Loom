using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Loom.Results;

/// <summary>
/// Distinguishes a result that was never initialized from one that succeeded or failed.
/// </summary>
/// <remarks>
/// Every struct has a <c>default</c>, and <c>default</c> of a result is neither a success nor a
/// failure. Reserving zero for that case lets the result types detect it and throw, rather than
/// silently reporting failure.
/// </remarks>
internal enum ResultState : byte
{
    Uninitialized = 0,
    Success = 1,
    Failure = 2,
}

/// <summary>
/// The outcome of an operation that can fail and produces no value.
/// </summary>
/// <remarks>
/// Never serialize a result. It is a control-flow type; response types cross the wire. Serializers
/// reflect over public members, and reading <see cref="Error" /> on a success throws.
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public readonly struct Result : IEquatable<Result>
{
    private readonly ResultState _state;
    private readonly Error? _error;

    private Result(ResultState state, Error? error)
    {
        _state = state;
        _error = error;
    }

    /// <summary>
    /// Gets a successful outcome.
    /// </summary>
    public static Result Success { get; } = new(ResultState.Success, error: null);

    /// <summary>
    /// Creates a failed outcome.
    /// </summary>
    /// <param name="error">The failure. Must not be <see langword="null" />.</param>
    /// <returns>A failed result carrying <paramref name="error" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error" /> is <see langword="null" />.</exception>
    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(ResultState.Failure, error);
    }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    /// <exception cref="InvalidOperationException">This result was never initialized.</exception>
    public bool IsSuccess => State is ResultState.Success;

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    /// <exception cref="InvalidOperationException">This result was never initialized.</exception>
    public bool IsFailure => State is ResultState.Failure;

    /// <summary>
    /// Gets the failure.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The operation succeeded, or this result was never initialized. Reading the failure of a
    /// successful result is a programming error, not an expected failure.
    /// </exception>
    public Error Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("This Result succeeded, so it has no Error. Check IsFailure first.");

    /// <summary>
    /// Invokes whichever of the two functions matches this result's outcome.
    /// </summary>
    /// <typeparam name="TOut">The type produced by both functions.</typeparam>
    /// <param name="onSuccess">Invoked when the operation succeeded.</param>
    /// <param name="onFailure">Invoked with the failure when the operation failed.</param>
    /// <returns>Whatever the invoked function returned.</returns>
    /// <exception cref="ArgumentNullException">Either function is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">This result was never initialized.</exception>
    public TOut Match<TOut>(Func<TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess() : onFailure(_error!);
    }

    /// <summary>
    /// Converts a failure into a failed result, so that a failure can be returned directly.
    /// </summary>
    /// <param name="error">The failure.</param>
    public static implicit operator Result(Error error) => Failure(error);

    /// <inheritdoc />
    public bool Equals(Result other) => _state == other._state && Equals(_error, other._error);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(_state, _error);

    /// <summary>Compares two results for equality.</summary>
    /// <param name="left">The first result.</param>
    /// <param name="right">The second result.</param>
    /// <returns><see langword="true" /> if the results are equal.</returns>
    public static bool operator ==(Result left, Result right) => left.Equals(right);

    /// <summary>Compares two results for inequality.</summary>
    /// <param name="left">The first result.</param>
    /// <param name="right">The second result.</param>
    /// <returns><see langword="true" /> if the results differ.</returns>
    public static bool operator !=(Result left, Result right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => _state switch
    {
        ResultState.Success => "Success",
        ResultState.Failure => $"Failure: {_error!.Code}",
        _ => "Uninitialized",
    };

    private ResultState State => _state is not ResultState.Uninitialized
        ? _state
        : throw new InvalidOperationException(
            "This Result was never initialized. Use Result.Success or Result.Failure.");

    private string DebuggerDisplay => ToString();
}

/// <summary>
/// The outcome of an operation that can fail and produces a value.
/// </summary>
/// <typeparam name="T">The type of the value produced on success.</typeparam>
/// <remarks>
/// Never serialize a result. It is a control-flow type; response types cross the wire. Serializers
/// reflect over public members, and reading <see cref="Value" /> on a failure throws.
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public readonly struct Result<T> : IEquatable<Result<T>>
{
    private readonly ResultState _state;
    private readonly T _value;
    private readonly Error? _error;

    private Result(T value)
    {
        _state = ResultState.Success;
        _value = value;
        _error = null;
    }

    private Result(Error error)
    {
        _state = ResultState.Failure;
        _value = default!;
        _error = error;
    }

    /// <summary>
    /// Creates a successful outcome.
    /// </summary>
    /// <param name="value">The value produced. Must not be <see langword="null" />.</param>
    /// <returns>A successful result carrying <paramref name="value" />.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="value" /> is <see langword="null" />. A successful result never carries
    /// <see langword="null" />: absence is either a <see cref="ErrorCategory.NotFound" /> failure or
    /// a nullable member of the value's own type.
    /// </exception>
    public static Result<T> Success(T value)
    {
        // typeof(T).IsValueType is a JIT-time constant, so this branch is erased entirely when T is
        // a value type. Calling ArgumentNullException.ThrowIfNull, or testing `value is null`
        // directly, would box the value on every successful result.
        if (!typeof(T).IsValueType && value is null)
        {
            throw new ArgumentNullException(
                nameof(value),
                "A successful Result never carries null. Model absence as a NotFound failure, or as a "
                + "nullable member of the value's own type.");
        }

        return new Result<T>(value);
    }

    /// <summary>
    /// Creates a failed outcome.
    /// </summary>
    /// <param name="error">The failure. Must not be <see langword="null" />.</param>
    /// <returns>A failed result carrying <paramref name="error" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error" /> is <see langword="null" />.</exception>
    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(error);
    }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    /// <exception cref="InvalidOperationException">This result was never initialized.</exception>
    public bool IsSuccess => State is ResultState.Success;

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    /// <exception cref="InvalidOperationException">This result was never initialized.</exception>
    public bool IsFailure => State is ResultState.Failure;

    /// <summary>
    /// Gets the value produced by the operation.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The operation failed, or this result was never initialized. Reading the value of a failed
    /// result is a programming error, not an expected failure.
    /// </exception>
    public T Value => IsSuccess
        ? _value
        : throw new InvalidOperationException("This Result failed, so it has no Value. Check IsSuccess first.");

    /// <summary>
    /// Gets the failure.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The operation succeeded, or this result was never initialized.
    /// </exception>
    public Error Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("This Result succeeded, so it has no Error. Check IsFailure first.");

    /// <summary>
    /// Gets the produced value, if the operation succeeded.
    /// </summary>
    /// <param name="value">The produced value, or <see langword="default" /> if the operation failed.</param>
    /// <returns><see langword="true" /> if the operation succeeded; otherwise <see langword="false" />.</returns>
    /// <exception cref="InvalidOperationException">This result was never initialized.</exception>
    public bool TryGetValue([NotNullWhen(true)] out T? value)
    {
        if (IsSuccess)
        {
            // Success guarantees a non-null value; flow analysis cannot see that guard through an
            // unconstrained T.
            value = _value!;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Invokes whichever of the two functions matches this result's outcome.
    /// </summary>
    /// <typeparam name="TOut">The type produced by both functions.</typeparam>
    /// <param name="onSuccess">Invoked with the value when the operation succeeded.</param>
    /// <param name="onFailure">Invoked with the failure when the operation failed.</param>
    /// <returns>Whatever the invoked function returned.</returns>
    /// <exception cref="ArgumentNullException">Either function is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">This result was never initialized.</exception>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess(_value) : onFailure(_error!);
    }

    /// <summary>
    /// Converts a value into a successful result, so that a value can be returned directly.
    /// </summary>
    /// <param name="value">The value produced.</param>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>
    /// Converts a failure into a failed result, so that a failure can be returned directly.
    /// </summary>
    /// <param name="error">The failure.</param>
    public static implicit operator Result<T>(Error error) => Failure(error);

    /// <summary>
    /// Discards the value, keeping only the outcome.
    /// </summary>
    /// <param name="result">The result to convert.</param>
    public static implicit operator Result(Result<T> result) =>
        result.IsSuccess ? Result.Success : Result.Failure(result._error!);

    /// <inheritdoc />
    public bool Equals(Result<T> other) =>
        _state == other._state
        && Equals(_error, other._error)
        && EqualityComparer<T>.Default.Equals(_value, other._value);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(_state, _value, _error);

    /// <summary>Compares two results for equality.</summary>
    /// <param name="left">The first result.</param>
    /// <param name="right">The second result.</param>
    /// <returns><see langword="true" /> if the results are equal.</returns>
    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);

    /// <summary>Compares two results for inequality.</summary>
    /// <param name="left">The first result.</param>
    /// <param name="right">The second result.</param>
    /// <returns><see langword="true" /> if the results differ.</returns>
    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => _state switch
    {
        ResultState.Success => $"Success: {_value}",
        ResultState.Failure => $"Failure: {_error!.Code}",
        _ => "Uninitialized",
    };

    private ResultState State => _state is not ResultState.Uninitialized
        ? _state
        : throw new InvalidOperationException(
            "This Result was never initialized. Use Result<T>.Success or Result<T>.Failure.");

    private string DebuggerDisplay => ToString();
}
