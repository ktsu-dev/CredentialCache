namespace ktsu.CredentialCache;

using ktsu.StrongStrings;

/// <summary>
/// Represents a token used for credential authentication.
/// </summary>
public sealed record class CredentialToken : StrongStringAbstract<CredentialToken> { }

/// <summary>
/// Represents a credential that includes a token for authentication.
/// </summary>
public sealed class CredentialWithToken : Credential
{
	/// <summary>
	/// Gets the token used for authentication.
	/// </summary>
	public CredentialToken Token { get; init; } = new();
}
