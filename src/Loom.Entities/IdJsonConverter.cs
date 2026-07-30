using System.Text.Json;
using System.Text.Json.Serialization;

namespace Loom.Entities;

/// <summary>
/// Serializes an <see cref="Id{TEntity}" /> as the bare identity string rather than as an object
/// wrapping a value.
/// </summary>
/// <remarks>
/// Applied to <see cref="Id{TEntity}" /> by attribute, so consumers get the right shape without
/// registering anything. Without it, an identity would serialize as <c>{"value":"..."}</c> where
/// every client expects a string.
/// </remarks>
public sealed class IdJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Id<>);

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        Type entityType = typeToConvert.GetGenericArguments()[0];
        Type converterType = typeof(IdJsonConverter<>).MakeGenericType(entityType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

internal sealed class IdJsonConverter<TEntity> : JsonConverter<Id<TEntity>>
{
    public override Id<TEntity> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? value = reader.GetString();

        return Id<TEntity>.TryParse(value, provider: null, out Id<TEntity> id)
            ? id
            : throw new JsonException($"'{value}' is not a valid {typeof(TEntity).Name} identity.");
    }

    public override void Write(Utf8JsonWriter writer, Id<TEntity> value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.Value);
    }

    public override Id<TEntity> ReadAsPropertyName(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) => Read(ref reader, typeToConvert, options);

    public override void WriteAsPropertyName(
        Utf8JsonWriter writer,
        Id<TEntity> value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WritePropertyName(value.Value.ToString());
    }
}
