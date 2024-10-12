namespace ktsu.CredentialCache;

using ktsu.StrongStrings;

public sealed record class CredentialToken : StrongStringAbstract<CredentialToken> { }

public sealed class CredentialWithToken : Credential
{
	public CredentialToken Token { get; init; } = new();
}
