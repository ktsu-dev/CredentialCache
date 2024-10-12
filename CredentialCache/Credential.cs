namespace ktsu.CredentialCache;

using System.Text.Json.Serialization;

[JsonDerivedType(typeof(CredentialWithNothing), nameof(CredentialWithNothing))]
[JsonDerivedType(typeof(CredentialWithToken), nameof(CredentialWithToken))]
[JsonDerivedType(typeof(CredentialWithUsernamePassword), nameof(CredentialWithUsernamePassword))]
[JsonPolymorphic]
public abstract class Credential
{
}
