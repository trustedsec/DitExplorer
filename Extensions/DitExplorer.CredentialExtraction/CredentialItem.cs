using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.CredentialExtraction;

internal sealed class CredentialItem
{
	internal CredentialItem(
		IDirectoryNode node,
		string? logonName,
		string? upn,
		Credential credential
		)
	{
		Node = node;
		LogonName = logonName;
		Upn = upn;
		Credential = credential;
	}

	public IDirectoryNode Node { get; }
	public string DistinguishedName => this.Node.Object.DistinguishedName;
	public string? LogonName { get; }
	public string? Upn { get; }
	public Credential Credential { get; }
	public string Label => GetLabelForCredentialType(this.CredentialType);
	public string PwdumpLabel => GetPwdumpLabel(this.CredentialType);

	public CredentialType CredentialType => this.Credential.CredentialType;
	public string? Text => this.Credential.Text;

	public static string? GetPwdumpLabel(CredentialType credentialType)
	{
		var group = (credentialType & CredentialType.GroupMask);
		if (group != 0)
		{
			CredentialType index = credentialType & CredentialType.IndexMask;
			return group switch
			{
				CredentialType.WDigestGroup => $"wdigest-{(int)(credentialType & CredentialType.IndexMask)}",
				CredentialType.KerberosOld => "old-" + GetPwdumpLabel(credentialType & CredentialType.IndexMask),
				// These are handled specially by the PWDump exporter
				CredentialType.LmHistory => null,
				CredentialType.NtHistory => null,
				_ => null,
			};
		}
		else
		{

			return credentialType switch
			{
				CredentialType.Password => "CLEARTEXT",
				CredentialType.KerberosDecCbcCrc => "kerb-enc-des-cbc-crc",
				CredentialType.KerberosDesCbcMd5 => "kerb-enc-des-cbc-md5",
				CredentialType.KerberosAes128CtsHmacSha1_96 => "kerb-enc-aes128-cts-hmac-sha1-96",
				CredentialType.KerberosAes256CtsHmacSha1_96 => "kerb-enc-aes256-cts-hmac-sha1-96",
				_ => null
			};
		}
	}

	public static string? GetLabelForCredentialType(CredentialType credentialType)
	{
		var group = (credentialType & CredentialType.GroupMask);
		if (group != 0)
		{
			CredentialType index = credentialType & CredentialType.IndexMask;
			return group switch
			{
				CredentialType.WDigestGroup => Messages.Credential_WDigest + $"({(int)index})",
				CredentialType.KerberosOld => GetLabelForCredentialType(index) + " [old]",
				CredentialType.LmHistory => Messages.Credential_LmHistory + " - " + (int)index,
				CredentialType.NtHistory => Messages.Credential_NtHistory + " - " + (int)index,
				_ => null
			};
		}
		else
		{
			return credentialType switch
			{
				CredentialType.LmHash => Messages.Credential_LmHash,
				CredentialType.NtHash => Messages.Credential_NtHash,
				CredentialType.StrongNtowfKey => Messages.Credential_StrongNtowfKey,
				CredentialType.Password => Messages.Credential_CleartextPassword,
				CredentialType.KerberosSalt => Messages.Credential_KerberosSalt,
				CredentialType.KerberosDecCbcCrc => Messages.Credential_KerberosDecCbcCrc,
				CredentialType.KerberosDesCbcMd5 => Messages.Credential_KerberosDesCbcMd5,
				CredentialType.KerberosAes128CtsHmacSha1_96 => Messages.Credential_KerberosAes128CtsHmacSha1_96,
				CredentialType.KerberosAes256CtsHmacSha1_96 => Messages.Credential_KerberosAes256CtsHmacSha1_96,
				_ => $"(other: {(int)credentialType})"
			};
		}
	}
}
