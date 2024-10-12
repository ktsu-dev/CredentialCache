namespace ktsu.CredentialCache;

using System.Text.Json.Serialization;

[JsonDerivedType(typeof(CredentialNone), nameof(CredentialNone))]
[JsonDerivedType(typeof(CredentialToken), nameof(CredentialToken))]
[JsonDerivedType(typeof(CredentialUsernamePassword), nameof(CredentialUsernamePassword))]
[JsonPolymorphic]
public abstract class Credential
{
}
