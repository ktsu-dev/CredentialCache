// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CredentialCache;

using ktsu.Semantics.Strings;

/// <summary>
/// Represents a token used for credential authentication.
/// </summary>
public sealed record class CredentialToken : SemanticString<CredentialToken> { }

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
