// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CredentialCache.Test;

using ktsu.CredentialCache.Storage;
using ktsu.Semantics.Strings;

[TestClass]
public class CredentialCacheTests
{
	private static CredentialCache NewCache() => new(new InMemoryCredentialStore());

	[TestMethod]
	public void TryGetReturnsFalseWhenCredentialNotFound()
	{
		using CredentialCache cache = NewCache();
		PersonaGUID guid = CredentialCache.CreatePersonaGUID();

		bool result = cache.TryGet(guid, out Credential? credential);

		Assert.IsFalse(result, "TryGet should return false when credential is not found");
		Assert.IsNull(credential);
	}

	[TestMethod]
	public void AddOrReplaceAddsCredentialSuccessfully()
	{
		using CredentialCache cache = NewCache();
		PersonaGUID guid = CredentialCache.CreatePersonaGUID();
		CredentialWithNothing credential = new();

		cache.AddOrReplace(guid, credential);
		bool result = cache.TryGet(guid, out Credential? retrievedCredential);

		Assert.IsTrue(result, "TryGet should return true after adding a credential");
		Assert.AreEqual(credential, retrievedCredential);
	}

	[TestMethod]
	public void AddOrReplacePersistsCredentialToBackingStore()
	{
		InMemoryCredentialStore store = new();
		PersonaGUID guid = CredentialCache.CreatePersonaGUID();
		CredentialWithUsernamePassword credential = new()
		{
			Username = SemanticString<CredentialUsername>.Create("alice"),
			Password = SemanticString<CredentialPassword>.Create("hunter2"),
		};

		using (CredentialCache writer = new(store))
		{
			writer.AddOrReplace(guid, credential);
		}

		using CredentialCache reader = new(store);
		Assert.IsTrue(reader.TryGet(guid, out Credential? roundTripped));
		CredentialWithUsernamePassword? typed = roundTripped as CredentialWithUsernamePassword;
		Assert.IsNotNull(typed);
		Assert.AreEqual("alice", typed!.Username.ToString());
		Assert.AreEqual("hunter2", typed.Password.ToString());
	}

	[TestMethod]
	public void RemoveDeletesCredentialFromBothLayers()
	{
		InMemoryCredentialStore store = new();
		PersonaGUID guid = CredentialCache.CreatePersonaGUID();
		CredentialWithNothing credential = new();

		using CredentialCache cache = new(store);
		cache.AddOrReplace(guid, credential);
		Assert.IsTrue(cache.Remove(guid));
		Assert.IsFalse(cache.TryGet(guid, out _));
		Assert.IsFalse(store.TryLoad(guid, out _));
	}

	[TestMethod]
	public void TryCreateReturnsFalseWhenFactoryNotRegistered()
	{
		using CredentialCache cache = NewCache();

		bool result = cache.TryCreate<CredentialWithNothing>(out Credential? credential);

		Assert.IsFalse(result, "TryCreate should return false when factory is not registered");
		Assert.IsNull(credential);
	}

	[TestMethod]
	public void TryCreateCreatesCredentialSuccessfullyWhenFactoryRegistered()
	{
		using CredentialCache cache = NewCache();
		cache.RegisterCredentialFactory(new CredentialWithNothingFactory());

		bool result = cache.TryCreate<CredentialWithNothing>(out Credential? credential);

		Assert.IsTrue(result, "TryCreate should return true when factory is registered");
		Assert.IsNotNull(credential);
		Assert.IsInstanceOfType<CredentialWithNothing>(credential);
	}

	[TestMethod]
	public void UnregisterCredentialFactoryUnregistersFactorySuccessfully()
	{
		using CredentialCache cache = NewCache();
		cache.RegisterCredentialFactory(new CredentialWithNothingFactory());

		cache.UnregisterCredentialFactory<CredentialWithNothing>();
		bool result = cache.TryCreate<CredentialWithNothing>(out Credential? credential);

		Assert.IsFalse(result, "TryCreate should return false after unregistering factory");
		Assert.IsNull(credential);
	}

	[TestMethod]
	public void UnregisterCredentialFactoryDoesNothingWhenFactoryNotRegistered()
	{
		using CredentialCache cache = NewCache();

		cache.UnregisterCredentialFactory<CredentialWithNothing>();
		bool result = cache.TryCreate<CredentialWithNothing>(out Credential? credential);

		Assert.IsFalse(result);
		Assert.IsNull(credential);
	}

	[TestMethod]
	public void RegisterCredentialFactoryThrowsArgumentNullExceptionWhenFactoryIsNull()
	{
		using CredentialCache cache = NewCache();
		ICredentialFactory<CredentialWithNothing>? factory = null;

		Assert.Throws<ArgumentNullException>(() => cache.RegisterCredentialFactory(factory!));
	}

	[TestMethod]
	public void AddOrReplaceThrowsArgumentNullExceptionWhenCredentialIsNull()
	{
		using CredentialCache cache = NewCache();
		PersonaGUID guid = CredentialCache.CreatePersonaGUID();
		CredentialWithNothing? credential = null;

		Assert.Throws<ArgumentNullException>(() => cache.AddOrReplace(guid, credential!));
	}

	[TestMethod]
	public void ConstructorThrowsWhenStoreIsNull()
	{
		ICredentialStore? store = null;
		Assert.Throws<ArgumentNullException>(() => new CredentialCache(store!));
	}

	[TestMethod]
	public void OperationsThrowAfterDispose()
	{
		CredentialCache cache = NewCache();
		cache.Dispose();

		Assert.Throws<ObjectDisposedException>(() =>
			cache.AddOrReplace(CredentialCache.CreatePersonaGUID(), new CredentialWithNothing()));
		Assert.Throws<ObjectDisposedException>(() =>
			cache.TryGet(CredentialCache.CreatePersonaGUID(), out _));
	}

	[TestMethod]
	public void CredentialSerializationRoundTripsAllKnownTypes()
	{
		Credential nothing = new CredentialWithNothing();
		Credential token = new CredentialWithToken
		{
			Token = SemanticString<CredentialToken>.Create("opaque-token"),
		};
		Credential creds = new CredentialWithUsernamePassword
		{
			Username = SemanticString<CredentialUsername>.Create("u"),
			Password = SemanticString<CredentialPassword>.Create("p"),
		};

		foreach (Credential original in new[] { nothing, token, creds })
		{
			byte[] bytes = CredentialSerialization.Serialize(original);
			Credential? deserialized = CredentialSerialization.Deserialize(bytes);
			Assert.IsNotNull(deserialized);
			Assert.AreEqual(original.GetType(), deserialized!.GetType());
		}
	}

	[TestMethod]
	public void RegisterMultipleCredentialFactoriesCreatesCredentialsCorrectly()
	{
		using CredentialCache cache = NewCache();
		CredentialWithNothingFactory factory1 = new();
		AnotherCredentialFactory factory2 = new();
		cache.RegisterCredentialFactory(factory1);
		cache.RegisterCredentialFactory(factory2);
		PersonaGUID guid1 = CredentialCache.CreatePersonaGUID();
		PersonaGUID guid2 = CredentialCache.CreatePersonaGUID();

		cache.AddOrReplace(guid1, factory1.Create());
		cache.AddOrReplace(guid2, factory2.Create());
		bool result1 = cache.TryGet(guid1, out Credential? credential1);
		bool result2 = cache.TryGet(guid2, out Credential? credential2);

		Assert.IsTrue(result1);
		Assert.IsInstanceOfType<CredentialWithNothing>(credential1);

		Assert.IsTrue(result2);
		Assert.IsInstanceOfType<AnotherCredential>(credential2);
	}

	[TestMethod]
	public void CredentialCacheIsThreadSafeUnderConcurrentAccess()
	{
		using CredentialCache cache = NewCache();
		cache.RegisterCredentialFactory(new CredentialWithNothingFactory());
		const int numberOfThreads = 8;
		const int operationsPerThread = 100;
		List<Task> tasks = [];

		for (int i = 0; i < numberOfThreads; i++)
		{
			tasks.Add(Task.Run(() =>
			{
				for (int j = 0; j < operationsPerThread; j++)
				{
					PersonaGUID guid = CredentialCache.CreatePersonaGUID();
					CredentialWithNothing credential = new();
					cache.AddOrReplace(guid, credential);
					Assert.IsTrue(cache.TryGet(guid, out Credential? retrieved));
					Assert.AreEqual(credential, retrieved);
				}
			}));
		}

		Task.WaitAll([.. tasks]);
	}

	[TestMethod]
	public void ConfigureStoreCannotBeCalledAfterInstanceConstructed()
	{
		CredentialCache.ResetSingletonForTesting();
		CredentialCache.ConfigureStore(new InMemoryCredentialStore());
		_ = CredentialCache.Instance;
		Assert.Throws<InvalidOperationException>(
			() => CredentialCache.ConfigureStore(new InMemoryCredentialStore()));
		CredentialCache.ResetSingletonForTesting();
	}

	[TestMethod]
	public void SingletonUsesConfiguredStore()
	{
		CredentialCache.ResetSingletonForTesting();
		InMemoryCredentialStore store = new();
		CredentialCache.ConfigureStore(store);
		try
		{
			CredentialCache instance = CredentialCache.Instance;
			Assert.AreSame(store, instance.Store);
		}
		finally
		{
			CredentialCache.ResetSingletonForTesting();
		}
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
