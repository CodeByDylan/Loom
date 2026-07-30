using System.ComponentModel.DataAnnotations;

namespace Ordering.Api.Infrastructure;

/// <summary>
/// Token validation settings.
/// </summary>
/// <remarks>
/// A symmetric key issued by this sample stands in for a real identity provider, so that the shape of
/// the authorization rules can be demonstrated without the sample choosing an provider on anyone's
/// behalf. A real deployment validates tokens from an external issuer and holds no signing key at all.
/// <para>
/// Validated at startup, so a misconfigured application fails to boot rather than failing on the
/// first request that needs a token.
/// </para>
/// </remarks>
public sealed class AuthenticationOptions
{
    /// <summary>The configuration section these settings are bound from.</summary>
    public const string SectionName = "Authentication";

    /// <summary>
    /// The key committed in <c>appsettings.Development.json</c> so the sample runs without setup.
    /// </summary>
    /// <remarks>
    /// Named here so that startup can refuse it outside Development. A key in source control is public
    /// by definition, and a deployment that inherited one would validate tokens anybody could have
    /// signed — so this is rejected rather than merely warned about.
    /// </remarks>
    public const string DevelopmentSigningKey = "development-only-key-not-valid-outside-development";

    /// <summary>
    /// Gets the symmetric key incoming tokens are validated against.
    /// </summary>
    /// <remarks>
    /// Comes from user secrets or the environment outside Development, and is never committed. The
    /// length constraint is what HMAC-SHA256 needs, not an arbitrary minimum.
    /// </remarks>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>Gets the issuer a token must name for it to be accepted.</summary>
    [Required]
    public string Issuer { get; init; } = string.Empty;

    /// <summary>Gets the audience a token must name for it to be accepted.</summary>
    [Required]
    public string Audience { get; init; } = string.Empty;
}
