namespace ktsu.CredentialCache;

using ktsu.StrongStrings;

public sealed record class CredentialUsername : StrongStringAbstract<CredentialUsername> { }
public sealed record class CredentialPassword : StrongStringAbstract<CredentialPassword> { }

public sealed class CredentialWithUsernamePassword : Credential
{
	public CredentialUsername Username { get; init; } = new();
	public CredentialPassword Password { get; init; } = new();
}
