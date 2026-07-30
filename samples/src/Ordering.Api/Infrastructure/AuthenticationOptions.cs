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
    public const string SectionName = "Authentication";

    [Required]
    [MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;
}
