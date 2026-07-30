using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;
using Ordering.Api.Infrastructure;

namespace Ordering.Api.Tests;

/// <summary>
/// The configuration a deployment must not inherit is refused while the host is being built.
/// </summary>
/// <remarks>
/// The development signing key is committed so the sample runs without setup, which makes it public.
/// A check that only warned would leave a deployment validating tokens anyone could have signed, and
/// nothing about the running application would look wrong — so it fails to start instead.
/// </remarks>
[NotInParallel]
public sealed class StartupValidationTests
{
    [Test]
    public async Task The_Development_Signing_Key_Is_Refused_Outside_Development()
    {
        await using WebApplicationFactory<Program> factory = ApiFixture.Api
            .FactoryFor("Production", AuthenticationOptions.DevelopmentSigningKey);

        await Assert.That(factory.CreateClient).Throws<OptionsValidationException>();
    }

    [Test]
    public async Task A_Real_Signing_Key_Is_Accepted_Outside_Development()
    {
        await using WebApplicationFactory<Program> factory = ApiFixture.Api
            .FactoryFor("Production", "a-key-that-is-not-the-committed-one-and-long-enough");

        // The check is about which key, not about the environment: a deployment that supplied its own
        // must still start, or the guard would simply be a ban on running outside Development.
        using HttpClient client = factory.CreateClient();

        await Assert.That(client).IsNotNull();
    }
}
