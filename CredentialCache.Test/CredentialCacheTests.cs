namespace ktsu.CredentialCache.Test;

[TestClass]
//[DoNotParallelize]
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
		cache.UnregisterCredentialFactory<CredentialWithNothing>(); //ensure factory is not registered by some previous test

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
	public void UnregisterCredentialFactoryDoesNothingWhenFactoryNotRegistered()
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

	[TestMethod]
	public void RegisterCredentialFactoryThrowsArgumentNullExceptionWhenFactoryIsNull()
	{
		// Arrange
		var cache = CredentialCache.Instance;
		ICredentialFactory<CredentialWithNothing>? factory = null;

		// Act & Assert
		Assert.ThrowsException<ArgumentNullException>(() => cache.RegisterCredentialFactory(factory!));
	}

	[TestMethod]
	public void AddOrReplaceThrowsArgumentNullExceptionWhenCredentialIsNull()
	{
		// Arrange
		var cache = CredentialCache.Instance;
		var guid = CredentialCache.CreatePersonaGUID();
		CredentialWithNothing? credential = null;

		// Act & Assert
		Assert.ThrowsException<ArgumentNullException>(() => cache.AddOrReplace(guid, credential!));
	}

	[TestMethod]
	public void RegisterMultipleCredentialFactoriesCreatesCredentialsCorrectly()
	{
		// Arrange
		var cache = CredentialCache.Instance;
		var factory1 = new CredentialWithNothingFactory();
		var factory2 = new AnotherCredentialFactory();
		cache.RegisterCredentialFactory(factory1);
		cache.RegisterCredentialFactory(factory2);
		var guid1 = CredentialCache.CreatePersonaGUID();
		var guid2 = CredentialCache.CreatePersonaGUID();

		// Act
		cache.AddOrReplace(guid1, factory1.Create());
		cache.AddOrReplace(guid2, factory2.Create());
		bool result1 = cache.TryGet(guid1, out var credential1);
		bool result2 = cache.TryGet(guid2, out var credential2);

		// Assert
		Assert.IsTrue(result1);
		Assert.IsNotNull(credential1);
		Assert.IsInstanceOfType<CredentialWithNothing>(credential1);

		Assert.IsTrue(result2);
		Assert.IsNotNull(credential2);
		Assert.IsInstanceOfType<AnotherCredential>(credential2);
	}

	[TestMethod]
	public void CredentialCacheIsThreadSafeUnderConcurrentAccess()
	{
		// Arrange
		var cache = CredentialCache.Instance;
		var factory = new CredentialWithNothingFactory();
		cache.RegisterCredentialFactory(factory);
		int numberOfThreads = 10;
		int operationsPerThread = 100;
		List<Task> tasks = [];

		// Act
		for (int i = 0; i < numberOfThreads; i++)
		{
			tasks.Add(Task.Run(() =>
			{
				for (int j = 0; j < operationsPerThread; j++)
				{
					var guid = CredentialCache.CreatePersonaGUID();
					var credential = factory.Create();
					cache.AddOrReplace(guid, credential);
					bool result = cache.TryGet(guid, out var retrievedCredential);
					Assert.IsTrue(result);
					Assert.AreEqual(credential, retrievedCredential);
				}
			}));
		}

		Task.WaitAll([.. tasks]);

		// Assert
		// All assertions within tasks are validated
	}

	[TestMethod]
	public void UnregisterCredentialFactoryPreventsCredentialCreation()
	{
		// Arrange
		var cache = CredentialCache.Instance;
		var factory = new CredentialWithNothingFactory();
		cache.RegisterCredentialFactory(factory);
		var guid = CredentialCache.CreatePersonaGUID();
		cache.AddOrReplace(guid, factory.Create());

		// Act
		cache.UnregisterCredentialFactory<CredentialWithNothing>();
		bool creationResult = cache.TryCreate<CredentialWithNothing>(out var credentialAfterUnregister);
		bool retrievalResult = cache.TryGet(guid, out var retrievedCredential);

		// Assert
		Assert.IsFalse(creationResult);
		Assert.IsNull(credentialAfterUnregister);
		Assert.IsTrue(retrievalResult);
		Assert.IsInstanceOfType<CredentialWithNothing>(retrievedCredential);
	}
}

public class CredentialWithNothingFactory : ICredentialFactory<CredentialWithNothing>
{
	public CredentialWithNothing Create() => new();
}

public class AnotherCredential : Credential { }

public class AnotherCredentialFactory : ICredentialFactory<AnotherCredential>
{
	public AnotherCredential Create() => new();
}


