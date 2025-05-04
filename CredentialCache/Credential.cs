// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CredentialCache;

using System.Text.Json.Serialization;

/// <summary>
/// Represents an abstract base class for different types of credentials.
/// </summary>
[JsonDerivedType(typeof(CredentialWithNothing), nameof(CredentialWithNothing))]
[JsonDerivedType(typeof(CredentialWithToken), nameof(CredentialWithToken))]
[JsonDerivedType(typeof(CredentialWithUsernamePassword), nameof(CredentialWithUsernamePassword))]
[JsonPolymorphic]
public abstract class Credential
{
}
