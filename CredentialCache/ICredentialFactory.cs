namespace ktsu.CredentialCache;

public interface ICredentialFactory { }

public interface ICredentialFactory<T>
	: ICredentialFactory
	where T : Credential
{
	T Create();
}
