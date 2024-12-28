namespace ktsu.CredentialCache.Test;

[TestClass]
public class CredentialCacheTests
{
	[TestMethod]
	public void TryGetReturnsFalseWhenCredentialNotFound()
	{
		// Arrange
		var cache = CredentialCache.Instance;
		var guid = CredentialCache.CreatePersonaGUID();

		// Act
		bool result = cache.TryGet(guid, out var credential);

		// Assert
		Assert.IsFalse(result);
		Assert.IsNull(credential);
	}

	[TestMethod]
	public void AddOrReplaceAddsCredentialSuccessfully()
	{
		// Arrange
		var cache = CredentialCache.Instance;
		var guid = CredentialCache.CreatePersonaGUID();
		var credential = new CredentialWithNothing();

		// Act
		cache.AddOrReplace(guid, credential);
		bool result = cache.TryGet(guid, out var retrievedCredential);

		// Assert
		Assert.IsTrue(result);
		Assert.AreEqual(credential, retrievedCredential);
	}

	[TestMethod]
	public void TryCreateReturnsFalseWhenFactoryNotRegistered()
	{
		// Arrange
		var cache = CredentialCache.Instance;

		// Act
		bool result = cache.TryCreate<CredentialWithNothing>(out var credential);

		// Assert
		Assert.IsFalse(result);
		Assert.IsNull(credential);
	}

	[TestMethod]
	public void TryCreateCreatesCredentialSuccessfullyWhenFactoryRegistered()
	{
		// Arrange
		var cache = CredentialCache.Instance;
		var factory = new CredentialWithNothingFactory();
		cache.RegisterCredentialFactory(factory);

		// Act
		bool result = cache.TryCreate<CredentialWithNothing>(out var credential);

		// Assert
		Assert.IsTrue(result);
		Assert.IsNotNull(credential);
		Assert.IsInstanceOfType<CredentialWithNothing>(credential);
	}

	[TestMethod]
	public void RegisterCredentialFactoryRegistersFactorySuccessfully()
	{
		// Arrange
		var cache = CredentialCache.Instance;
		var factory = new CredentialWithNothingFactory();

		// Act
		cache.RegisterCredentialFactory(factory);
		bool result = cache.TryCreate<CredentialWithNothing>(out var credential);

		// Assert
		Assert.IsTrue(result);
		Assert.IsNotNull(credential);
		Assert.IsInstanceOfType<CredentialWithNothing>(credential);
	}

	[TestMethod]
	public void UnregisterCredentialFactoryUnregistersFactorySuccessfully()
	{
		// Arrange
		var cache = CredentialCache.Instance;
		var factory = new CredentialWithNothingFactory();
		cache.RegisterCredentialFactory(factory);

		// Act
		cache.UnregisterCredentialFactory<CredentialWithNothing>();
		bool result = cache.TryCreate<CredentialWithNothing>(out var credential);

		// Assert
		Assert.IsFalse(result);
		Assert.IsNull(credential);
	}

	[TestMethod]
	public void UnregisterCredentialFactoryDoesNothing_WhenFactoryNotRegistered()
	{
		// Arrange
		var cache = CredentialCache.Instance;

		// Act
		cache.UnregisterCredentialFactory<CredentialWithNothing>();
		bool result = cache.TryCreate<CredentialWithNothing>(out var credential);

		// Assert
		Assert.IsFalse(result);
		Assert.IsNull(credential);
	}

}

public class CredentialWithNothingFactory : ICredentialFactory<CredentialWithNothing>
{
	public CredentialWithNothing Create() => new();
}
