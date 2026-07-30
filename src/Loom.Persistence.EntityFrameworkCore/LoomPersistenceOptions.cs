using Microsoft.Extensions.DependencyInjection;

namespace Loom.Persistence;

/// <summary>
/// Configures optional persistence behaviour.
/// </summary>
public sealed class LoomPersistenceOptions
{
    private readonly List<Action<IServiceCollection>> _registrations = [];

    internal void AddRegistration(Action<IServiceCollection> registration) => _registrations.Add(registration);

    internal void Apply(IServiceCollection services)
    {
        foreach (Action<IServiceCollection> registration in _registrations)
        {
            registration(services);
        }
    }
}
