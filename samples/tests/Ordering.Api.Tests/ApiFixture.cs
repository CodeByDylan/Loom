namespace Ordering.Api.Tests;

/// <summary>
/// Starts one container and one migrated database for the whole assembly.
/// </summary>
/// <remarks>
/// A container per test would make the suite unusable, and sharing one across parallel tests would
/// race with the reset between them. So the container is assembly-scoped and the tests that use it are
/// serialised, which is the trade the guidance describes: slice tests stay fast, and fidelity comes
/// from a real database rather than from a substitute.
/// </remarks>
public static class ApiFixture
{
    private static OrderingApi? _api;

    public static OrderingApi Api => _api
        ?? throw new InvalidOperationException("The fixture has not been started.");

    [Before(Assembly)]
    public static async Task StartAsync() => _api = await OrderingApi.StartAsync();

    [After(Assembly)]
    public static async Task StopAsync()
    {
        if (_api is not null)
        {
            await _api.DisposeAsync();
            _api = null;
        }
    }
}
