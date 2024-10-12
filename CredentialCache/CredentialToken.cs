namespace ktsu.CredentialCache;

using ktsu.StrongStrings;

public sealed record class GitToken : StrongStringAbstract<GitToken> { }

public sealed class CredentialToken : Credential
{
	public GitToken Token { get; init; } = new();
}
