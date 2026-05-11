// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CredentialCache.Storage;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Raised when an operation against the underlying OS credential store fails.
/// </summary>
[SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "Always raised with detail.")]
public sealed class CredentialStoreException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CredentialStoreException"/> class.
	/// </summary>
	public CredentialStoreException(string message) : base(message) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="CredentialStoreException"/> class.
	/// </summary>
	public CredentialStoreException(string message, Exception innerException) : base(message, innerException) { }
}
