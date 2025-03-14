namespace DitExplorer.CredentialExtraction;

public enum CredentialType
{
	Unspecified = 0,

	LmHash,
	NtHash,
	StrongNtowfKey,
	Password,
	KerberosSalt,
	KerberosDecCbcCrc,
	KerberosDesCbcMd5,
	KerberosAes128CtsHmacSha1_96,
	KerberosAes256CtsHmacSha1_96,

	// These types of credentials can have multiple values
	WDigestGroup = 0x100,
	KerberosOld = 0x200,
	LmHistory = 0x300,
	NtHistory = 0x400,

	GroupMask = 0xFF00,
	IndexMask = 0xFF,
}

/// <summary>
/// Describes a credential in the directory.
/// </summary>
public abstract class Credential
{
	protected Credential(CredentialType credentialType, string? errorDetails)
	{
		this.CredentialType = credentialType;
		this.ErrorDetails = errorDetails;
	}

	public abstract string? Text { get; }
	/// <inheritdoc/>
	public sealed override string ToString() => this.Text;
	public CredentialType CredentialType { get; }
	public string? ErrorDetails { get; }
}
/// <summary>
/// Describes a credential expressed as a hash.
/// </summary>
public sealed class HashCredential : Credential
{
	public const string MissingLmHash = "aad3b435b51404eeaad3b435b51404ee";
	public const string MissingNtHash = "31d6cfe0d16ae931b73c59d7e0c089c0";

	internal HashCredential(CredentialType credentialType, byte[] hash)
		: base(credentialType, null)
	{
		this.Hash = hash;
	}
	internal HashCredential(CredentialType credentialType, string? errorDetails)
		: base(credentialType, errorDetails)
	{

	}

	private string? _text;
	public override string? Text => (this._text ??= this.Hash.ToHexString(HexStringOptions.Lowercase, string.Empty));
	public byte[]? Hash { get; }

}
/// <summary>
/// Describes a credential expressed as clear text.
/// </summary>
public sealed class CleartextCredential : Credential
{
	internal CleartextCredential(CredentialType credentialType, string text)
		: base(credentialType, null)
	{
		this.Text = text;
	}

	public override string? Text { get; }
}