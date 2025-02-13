namespace DitExplorer.CredentialExtraction;

public enum CredentialType
{
	LmHash,
	NtHash,
	Password,
	KerberosAes256,
}

/// <summary>
/// Describes a credential in the directory.
/// </summary>
public abstract class Credential
{
	protected Credential(IDirectoryObject obj, CredentialType credentialType, string? errorDetails)
	{
		this.Object = obj;
		this.CredentialType = credentialType;
		this.ErrorDetails = errorDetails;
	}

	public abstract string? Text { get; }
	public IDirectoryObject Object { get; }
	public CredentialType CredentialType { get; }
	public string Label => this.CredentialType switch
	{
		CredentialType.LmHash => Messages.Credential_LmHash,
		CredentialType.NtHash => Messages.Credential_NtHash,
		// TODO: It's coming
		//CredentialType.Password => "Clear-text password",
		//CredentialType.KerberosAes256 => "Kerberos AES-256 key",
		_ => "(other)"
	};
	public string? ErrorDetails { get; }
}
/// <summary>
/// Describes a credential expressed as a hash.
/// </summary>
public sealed class HashCredential : Credential
{
	internal HashCredential(IDirectoryObject obj, CredentialType credentialType, byte[] hash)
		: base(obj, credentialType, null)
	{
		this.Hash = hash;
	}
	internal HashCredential(IDirectoryObject obj, CredentialType credentialType, string? errorDetails)
		: base(obj, credentialType, errorDetails)
	{

	}

	private string? _text;
	public override string? Text => (this._text ??= this.Hash.ToHexString(HexStringOptions.Lowercase, string.Empty));
	public byte[]? Hash { get; }

}