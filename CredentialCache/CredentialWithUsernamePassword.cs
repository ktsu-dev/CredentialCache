// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CredentialCache;

using ktsu.StrongStrings;

/// <summary>
/// Represents a credential with a username.
/// </summary>
public sealed record class CredentialUsername : StrongStringAbstract<CredentialUsername> { }
/// <summary>
/// Represents a credential with a password.
/// </summary>
public sealed record class CredentialPassword : StrongStringAbstract<CredentialPassword> { }

/// <summary>
/// Represents a credential that includes both a username and a password.
/// </summary>
public sealed class CredentialWithUsernamePassword : Credential
{
	/// <summary>
	/// Gets the username associated with the credential.
	/// </summary>
	public CredentialUsername Username { get; init; } = new();

	/// <summary>
	/// Gets the password associated with the credential.
	/// </summary>
	public CredentialPassword Password { get; init; } = new();
}
