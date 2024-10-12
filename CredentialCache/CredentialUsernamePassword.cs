namespace ktsu.CredentialCache;

using ktsu.StrongStrings;

public sealed record class GitUsername : StrongStringAbstract<GitUsername> { }
public sealed record class GitPassword : StrongStringAbstract<GitPassword> { }

public sealed class CredentialUsernamePassword : Credential
{
	public GitUsername Username { get; init; } = new();
	public GitPassword Password { get; init; } = new();
}
