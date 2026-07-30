using System.Reflection;
using Loom.Entities;
using Microsoft.EntityFrameworkCore;

namespace Loom.Persistence;

/// <summary>
/// Teaches a model how to store Loom's identity type.
/// </summary>
public static class ModelConfigurationBuilderExtensions
{
    /// <summary>
    /// Registers a conversion for the identity of every entity found in the given assemblies, so
    /// that <see cref="Id{TEntity}" /> properties are stored as their underlying value.
    /// </summary>
    /// <param name="configurationBuilder">The convention configuration being built.</param>
    /// <param name="assemblies">The assemblies to search for entity types.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">No assemblies were given.</exception>
    /// <remarks>
    /// Call this from <c>ConfigureConventions</c>, not <c>OnModelCreating</c>. The distinction is not
    /// cosmetic: property discovery skips properties whose type it does not recognise, so an identity
    /// that is not a primary key never enters the model at all and there is nothing left to configure
    /// by the time <c>OnModelCreating</c> runs. Registering the conversion as a convention happens
    /// first and makes the type recognisable, so discovery picks such properties up.
    /// <para>
    /// Identities are found by looking for entity types rather than by scanning for closed
    /// <see cref="Id{TEntity}" /> types, because a foreign identity — an order holding a customer's
    /// identity — appears nowhere except as a property type.
    /// </para>
    /// <example>
    /// <code>
    /// protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
    ///     configurationBuilder.UseLoomIdentities(typeof(Order).Assembly);
    /// </code>
    /// </example>
    /// </remarks>
    public static ModelConfigurationBuilder UseLoomIdentities(
        this ModelConfigurationBuilder configurationBuilder,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        ArgumentNullException.ThrowIfNull(assemblies);

        if (assemblies.Length is 0)
        {
            throw new ArgumentException(
                "Name at least one assembly to search for entity types.",
                nameof(assemblies));
        }

        foreach (Type entityType in EntityTypesIn(assemblies))
        {
            configurationBuilder
                .Properties(typeof(Id<>).MakeGenericType(entityType))
                .HaveConversion(typeof(IdValueConverter<>).MakeGenericType(entityType));
        }

        return configurationBuilder;
    }

    // An entity declares itself by deriving from Entity<TSelf> with itself as the argument, so the
    // identity type follows from the entity type without any further registration.
    private static IEnumerable<Type> EntityTypesIn(IEnumerable<Assembly> assemblies) => assemblies
        .SelectMany(assembly => assembly.GetTypes())
        .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
        .Where(type => DerivesFromEntityOfItself(type))
        .Distinct();

    private static bool DerivesFromEntityOfItself(Type type)
    {
        for (Type? current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType
                && current.GetGenericTypeDefinition() == typeof(Entity<>)
                && current.GetGenericArguments()[0] == type)
            {
                return true;
            }
        }

        return false;
    }
}
