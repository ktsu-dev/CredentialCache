namespace ktsu.CredentialCache;

/// <summary>
/// Represents a factory for creating credentials.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "I'm using this as the base for the generic")]
public interface ICredentialFactory { }

/// <summary>
/// Represents a factory for creating credentials of a specific type.
/// </summary>
/// <typeparam name="T">The type of credential to create.</typeparam>
public interface ICredentialFactory<T> : ICredentialFactory where T : Credential
{
	/// <summary>
	/// Creates a new instance of the credential.
	/// </summary>
	/// <returns>A new instance of the credential of type <typeparamref name="T"/>.</returns>
	public T Create();
}
