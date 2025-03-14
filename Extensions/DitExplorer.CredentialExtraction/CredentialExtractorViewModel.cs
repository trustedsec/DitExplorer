using DitExplorer.EseInterop;
using DitExplorer.Registry;
using DitExplorer.UI;
using DitExplorer.UI.WpfApp;
using Microsoft.Isam.Esent.Interop;
using Microsoft.Win32;
using Microsoft.Windows.Themes;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using RegistryHive = DitExplorer.Registry.RegistryHive;

namespace DitExplorer.CredentialExtraction;

[Flags]
public enum CredentialExtractOptions
{
	IncludeNtLm = 1,
	IncludeNtLmHistory = 2,
	IncludeKerberos = 4,
	IncludeKerberosOld = 8,
	IncludeWDigest = 0x10,
	IncludeCleartextPassword = 0x20,
	IncludeStrongNtowfKey = 0x40,

	IncludeAllCredTypes = 0
		| IncludeNtLm
		| IncludeNtLmHistory
		| IncludeKerberos
		| IncludeKerberosOld
		| IncludeWDigest
		| IncludeCleartextPassword
		| IncludeStrongNtowfKey,

	SupplementalMask = 0
		| IncludeKerberos
		| IncludeKerberosOld
		| IncludeStrongNtowfKey
		| IncludeWDigest
		| IncludeCleartextPassword,

	Default = 0
		| IncludeNtLm
		| IncludeNtLmHistory
		| IncludeKerberos
		| IncludeKerberosOld
		| IncludeCleartextPassword
}

internal class CredentialExtractorViewModel : ViewModel
{
	public static ICommand ExtractCommand = new RoutedCommand("Tools.ExtractCredentials", typeof(CredentialExtractorViewModel));
	public static ICommand ExportCommand = new RoutedCommand("Tools.Export", typeof(CredentialExtractorViewModel));
	public static ICommand ExtractSyskeyCommand = new RoutedCommand("Tools.ExtractSyskey", typeof(CredentialExtractorViewModel));

	public CredentialExtractorViewModel(IDirectoryNode[] nodes)
	{
		if (nodes == null || nodes.Length == 0)
			throw new ArgumentException(nameof(nodes));

		Nodes = nodes;
		this.Directory = nodes[0].DirectoryView.Directory;

		this.RegisterCommand(ExtractCommand, Extract, CanExtract);
		this.RegisterCommand(ExportCommand, Export, CanExport);
		this.RegisterCommand(ExtractSyskeyCommand, ExtractSyskey);
		this.SystemKey = _savedSystemKey;
	}

	public string Title => Messages.CredentialExtractor_Title;
	public IDirectoryNode[] Nodes { get; }
	public IDirectory Directory { get; }

	private static string? _savedSystemKey;
	private byte[] _systemKeyBytes;
	private string _systemKey;
	public string SystemKey
	{
		get { return _systemKey; }
		set
		{
			if (this.NotifyIfChanged(ref _systemKey, value))
			{
				_systemKeyBytes = TryParseHexString(value);
				if (_systemKeyBytes != null)
					_savedSystemKey = value;

				CommandManager.InvalidateRequerySuggested();
			}
		}
	}

	private string? _systemKeyError;
	public string? SystemKeyError
	{
		get { return _systemKeyError; }
		private set => this.NotifyIfChanged(ref _systemKeyError, value);
	}


	private byte[]? TryParseHexString(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			SystemKeyError = null;
			return null;
		}
		else
		{
			if (value.Length != 32)
			{
				SystemKeyError = "The string must contain exactly 32 hex digits.";
				return null;
			}

			try
			{
				byte[] bytes = value.ParseHexString();
				SystemKeyError = null;
				return bytes;
			}
			catch (Exception ex)
			{
				SystemKeyError = ex.Message;
				return null;
			}
		}
	}

	private static byte[] DecryptPekList(byte[] pekListBytes, byte[] systemKey)
	{
		if (pekListBytes.Length < PekHeader.StructSize)
			throw new ArgumentException("The pekList data is too short.", nameof(pekListBytes));

		ref PekHeader hdr = ref MemoryMarshal.AsRef<PekHeader>(pekListBytes);
		var salt = hdr.salt.GetBytes();
		// TODO: Implement support for older versions
		//if (hdr.dwMajor < 3)
		//{
		//}
		if (hdr.versionMajor == 3)
		{
			var aes = Aes.Create();
			aes.Key = systemKey;
			var decrypted = aes.DecryptCbc(new ReadOnlySpan<byte>(pekListBytes).Slice(PekHeader.StructSize).ToArray(), salt.ToArray(), PaddingMode.None);
			return decrypted;
		}
		else
		{
			throw new NotSupportedException($"The pekList data reports version {hdr.versionMajor}.{hdr.versionMinor}, which is not supported by this implementation.");
		}
	}

	private IList<CredentialItem>? _creds;

	public IList<CredentialItem>? Credentials
	{
		get { return _creds; }
		set => this.NotifyIfChanged(ref _creds, value);
	}

	#region Options
	private static CredentialExtractOptions _defaultOptions = CredentialExtractOptions.Default;
	private CredentialExtractOptions _options = _defaultOptions;
	private bool GetOption(CredentialExtractOptions option)
		=> option == (this._options & option);
	private void SetOption(CredentialExtractOptions option, bool isSet)
	{
		if (isSet)
			this._options |= option;
		else
			this._options &= ~option;

		_defaultOptions = this._options;

		this.OnPropertyChanged(nameof(IncludeAll));
		this.OnPropertyChanged(nameof(IncludeNtLm));
		this.OnPropertyChanged(nameof(IncludeNtLmHistory));
		this.OnPropertyChanged(nameof(IncludeKerberos));
		this.OnPropertyChanged(nameof(IncludeKerberosOld));
		this.OnPropertyChanged(nameof(IncludeWDigest));
		this.OnPropertyChanged(nameof(IncludeCleartextPassword));
		this.OnPropertyChanged(nameof(IncludeStrongNtowfKey));
	}

	public bool IncludeAll
	{
		get => this.GetOption(CredentialExtractOptions.IncludeAllCredTypes);
		set => this.SetOption(CredentialExtractOptions.IncludeAllCredTypes, value);
	}
	public bool IncludeNtLm
	{
		get => this.GetOption(CredentialExtractOptions.IncludeNtLm);
		set => this.SetOption(CredentialExtractOptions.IncludeNtLm, value);
	}
	public bool IncludeNtLmHistory
	{
		get => this.GetOption(CredentialExtractOptions.IncludeNtLmHistory);
		set => this.SetOption(CredentialExtractOptions.IncludeNtLmHistory, value);
	}
	public bool IncludeKerberos
	{
		get => this.GetOption(CredentialExtractOptions.IncludeKerberos);
		set => this.SetOption(CredentialExtractOptions.IncludeKerberos, value);
	}
	public bool IncludeKerberosOld
	{
		get => this.GetOption(CredentialExtractOptions.IncludeKerberosOld);
		set => this.SetOption(CredentialExtractOptions.IncludeKerberosOld, value);
	}
	public bool IncludeWDigest
	{
		get => this.GetOption(CredentialExtractOptions.IncludeWDigest);
		set => this.SetOption(CredentialExtractOptions.IncludeWDigest, value);
	}
	public bool IncludeCleartextPassword
	{
		get => this.GetOption(CredentialExtractOptions.IncludeCleartextPassword);
		set => this.SetOption(CredentialExtractOptions.IncludeCleartextPassword, value);
	}
	public bool IncludeStrongNtowfKey
	{
		get => this.GetOption(CredentialExtractOptions.IncludeStrongNtowfKey);
		set => this.SetOption(CredentialExtractOptions.IncludeStrongNtowfKey, value);
	}
	#endregion

	private void Extract()
	{
		var systemKey = _systemKeyBytes;
		if (systemKey == null)
			return;

		var dir = Nodes[0].Object.Directory;

		var nodes = this.Nodes;
		this.Credentials = ExtractCredentials(systemKey, dir, nodes, this._options);
	}

	private static readonly byte[] PekListAuthenticator = new byte[]{
		0x56, 0xD9, 0x81, 0x48, 0xEC, 0x91, 0xD1, 0x11,
		0x90, 0x5A, 0x00, 0xC0, 0x4F, 0xC2, 0xD4, 0xCF
	};

	struct CredExtractContext
	{
		internal CredentialExtractOptions options;
		internal IAttributeSchema? attrDbcsPwd;
		internal IAttributeSchema? attrUnicodePwd;
		internal IAttributeSchema? attrLmHistory;
		internal IAttributeSchema? attrNtHistory;
		internal IAttributeSchema? attrSupplementalCredentials;
		internal IAttributeSchema? attrSid;
		internal byte[] sidBytes;
	}


	private void ExtractSyskey()
	{
		OpenFileDialog fd = new OpenFileDialog()
		{
			Title = "Extract from SYSTEM Hive",
			Filter = "Hive files (*.*)|*.*",
			FileName = "SYSTEM"
		};

		var res = fd.ShowDialog(this.Window);
		if (res ?? false)
		{
			var key = ExtractSystemKeyString(fd.FileName);
			this.SystemKey = key.ToHexString(HexStringOptions.Lowercase, separator: null);
		}
	}

	// TODO: Obfuscate
	private static readonly byte[] _BootKeyByteSwap = new byte[]{
		0x8, 0xa, 0x3, 0x7, 0x2, 0x1, 0x9, 0xF,
		0x0, 0x5, 0xD, 0x4, 0xB, 0x6, 0xC, 0xE,
	};

	private byte[] ExtractSystemKeyString(string hiveFileName)
	{
		byte[] hiveBytes = File.ReadAllBytes(hiveFileName);
		RegistryHive hive = new RegistryHive(hiveBytes);
		var rk = hive.RootKey;
		var rkLsa = rk.GetSubkey("ControlSet001")?.GetSubkey("Control")?.GetSubkey("Lsa");

		string[] names = { "JD", "Skew1", "GBG", "Data" };

		Span<byte> buf = stackalloc byte[4];
		byte[] syskeyBytes = new byte[16];

		for (int i = 0; i < names.Length; i++)
		{
			string? subkeyName = names[i];
			var sk = rkLsa.GetSubkey(subkeyName);
			sk.ClassName.ParseHexStringInto(buf);

			for (int j = 0; j < 4; j++)
			{
				syskeyBytes[_BootKeyByteSwap[i * 4 + j]] = buf[j];
			}
		}

		return syskeyBytes;
	}

	private static List<CredentialItem> ExtractCredentials(
		byte[] systemKey,
		IDirectory dir,
		IDirectoryNode[] nodes,
		CredentialExtractOptions options
		)
	{
		if (systemKey == null || systemKey.Length == 0)
			throw new ArgumentNullException(nameof(systemKey));
		if (systemKey.Length != 16)
			throw new ArgumentException("The system key is not the correct size.  It must be exactly 16 bytes in size.", nameof(systemKey));

		CredExtractContext context = new CredExtractContext
		{
			options = options,
			attrSupplementalCredentials = dir.TryGetAttributeByLdapName(CredentialAttributeNames.SupplementalCredentials),
			attrSid = dir.TryGetAttributeByLdapName(CredentialAttributeNames.ObjectSid),
			sidBytes = new byte[40]
		};

		if (0 != (options & CredentialExtractOptions.IncludeNtLm))
		{
			context.attrDbcsPwd = dir.TryGetAttributeByLdapName(CredentialAttributeNames.DbcsPwd);
			context.attrUnicodePwd = dir.TryGetAttributeByLdapName(CredentialAttributeNames.UnicodePwd);
		}

		if (0 != (options & CredentialExtractOptions.IncludeNtLmHistory))
		{
			context.attrLmHistory = dir.TryGetAttributeByLdapName(CredentialAttributeNames.LmPwdHistory);
			context.attrNtHistory = dir.TryGetAttributeByLdapName(CredentialAttributeNames.NtPwdHistory);
		}

		var attrUpn = dir.TryGetAttributeByLdapName(CredentialAttributeNames.UserPrincipalName);
		var attrAccountName = dir.TryGetAttributeByLdapName(CredentialAttributeNames.SamAccountName);

		var domain = dir.RootDomain;
		var attrPekList = dir.TryGetAttributeByLdapName(CredentialAttributeNames.PekList);
		var pekListBytes = (domain.GetValueOf(attrPekList) as BinaryData).Bytes;
		if (pekListBytes == null)
			throw new Exception("Unable to retrieve the pekList attribute from the domain.");

		pekListBytes = DecryptPekList(pekListBytes, systemKey);
		if (!CompareBytes(new ReadOnlySpan<byte>(pekListBytes, 0, PekListAuthenticator.Length), PekListAuthenticator))
			throw new InvalidDataException("Unable to verify the pekList with the key provided.  This most likely indicates that the system key is incorrect.");

		ref var pekData = ref MemoryMarshal.AsRef<PekEncryptedHeader>(pekListBytes);
		var keyListBytes = new ReadOnlySpan<byte>(pekListBytes).Slice(PekEncryptedHeader.StructSize);

		const int CredsPerItem = 5;
		List<CredentialItem> credItems = new List<CredentialItem>(nodes.Length * CredsPerItem);
		List<Credential> creds = new List<Credential>(CredsPerItem);
		foreach (var node in nodes)
		{
			creds.Clear();
			try
			{
				ExtractCredentialsForObject(node.Object, ref context, keyListBytes, creds);
				foreach (var cred in creds)
				{
					var logonName = node.Object.GetValueOf(attrAccountName) as string;
					var upn = node.Object.GetValueOf(attrUpn) as string;
					credItems.Add(new CredentialItem(node, logonName, upn, cred));
				}
			}
			catch (Exception ex)
			{
				// TODO: Report?
			}
		}

		return credItems;
	}

	private static void ExtractCredentialsForObject(
		IDirectoryObject item,
		ref CredExtractContext context,
		ReadOnlySpan<byte> keyListBytes,
		List<Credential> creds)
	{
		var sid = item.GetValueOf(context.attrSid) as SecurityIdentifier;
		if (sid != null)
		{
			var rid = sid.GetRid(ref context.sidBytes);

			// LM Hash
			if (context.attrDbcsPwd != null && (0 != (context.options & CredentialExtractOptions.IncludeNtLm)))
			{
				var credBytes = (item.GetValueOf(context.attrDbcsPwd) as BinaryData)?.Bytes;
				if (credBytes != null)
				{
					var cred = TryDecryptUserSecret(item, rid, credBytes, keyListBytes, CredentialType.LmHash);
					creds.Add(cred);
				}
			}
			// NT Hash
			if (context.attrUnicodePwd != null && (0 != (context.options & CredentialExtractOptions.IncludeNtLm)))
			{
				var credBytes = (item.GetValueOf(context.attrUnicodePwd) as BinaryData)?.Bytes;
				if (credBytes != null)
				{
					var cred = TryDecryptUserSecret(item, rid, credBytes, keyListBytes, CredentialType.NtHash);
					creds.Add(cred);
				}
			}
			// LM Hash history
			if (context.attrLmHistory != null && (0 != (context.options & CredentialExtractOptions.IncludeNtLmHistory)))
			{
				var credBytes = (item.GetValueOf(context.attrLmHistory) as BinaryData)?.Bytes;
				if (credBytes != null)
				{
					ExtractHistory(item, credBytes, rid, keyListBytes, CredentialType.LmHistory, creds);
				}
			}
			// NT Hash history
			if (context.attrNtHistory != null && (0 != (context.options & CredentialExtractOptions.IncludeNtLmHistory)))
			{
				var credBytes = (item.GetValueOf(context.attrNtHistory) as BinaryData)?.Bytes;
				if (credBytes != null)
				{
					ExtractHistory(item, credBytes, rid, keyListBytes, CredentialType.NtHistory, creds);
				}
			}

			// Other stuff like Kerberos or cleartext password
			if (0 != (context.options & CredentialExtractOptions.SupplementalMask))
			{
				var credBytes = (item.GetValueOf(context.attrSupplementalCredentials) as BinaryData)?.Bytes;
				if (credBytes != null)
				{
					var decryptedSecret = DecryptSecret(credBytes, keyListBytes);
					if (decryptedSecret.Length < USER_PROPERTIES.StructSize)
						throw new InvalidDataException("The user properties structure is not valid.");

					var propBytes = decryptedSecret.AsSpan().Slice(USER_PROPERTIES.StructSize);
					ref USER_PROPERTIES propsStruct = ref MemoryMarshal.AsRef<USER_PROPERTIES>(decryptedSecret);
					// TODO: Adjust for BE systems?
					if (propsStruct.propertySignature == USER_PROPERTIES.ValidPropertySignature)
					{
						for (int i = 0; i < propsStruct.propertyCount; i++)
						{
							if (propBytes.Length < USER_PROPERTY.StructSize)
								break;

							try
							{
								ref var propEntry = ref MemoryMarshal.AsRef<USER_PROPERTY>(propBytes);
								int offset = USER_PROPERTY.StructSize;
								var propName = Encoding.Unicode.GetString(propBytes.Slice(USER_PROPERTY.StructSize, propEntry.nameLength));
								offset += propEntry.nameLength;

								var propValueBytes = Encoding.ASCII.GetString(propBytes.Slice(offset, propEntry.valueLength)).ParseHexString();

								offset += propEntry.valueLength;

								object? effValue = null;
								// REF: https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-samr/0705f888-62e1-4a4c-bac0-b4d427f396f8
								switch (propName)
								{
									case "Primary:NTLM-Strong-NTOWF":
										if (0 != (context.options & CredentialExtractOptions.IncludeStrongNtowfKey))
										{
											var cred = new HashCredential(CredentialType.StrongNtowfKey, propValueBytes);
											creds.Add(cred);
										}
										break;
									case "Primary:CLEARTEXT":
										// REF: https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-samr/e348e08f-1c19-42bb-b1a9-687843059414
										if (0 != (context.options & CredentialExtractOptions.IncludeCleartextPassword))
										{
											var cred = ParseCleartextPassword(propValueBytes);
											creds.Add(cred);
										}
										break;
									case "Primary:WDigest":
										// REF: https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-samr/830b3962-3a00-42cf-94df-c192dcfd4803
										if (0 != (context.options & CredentialExtractOptions.IncludeWDigest))
										{
											ParseWDigestCreds(creds, propValueBytes);
										}
										break;
									case "Primary:Kerberos":
										// REF: https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-samr/866f0180-8694-4e14-be7e-dfd91d91694f
										if (0 != (context.options & (CredentialExtractOptions.IncludeKerberos | CredentialExtractOptions.IncludeKerberosOld)))
										{
											if (propValueBytes.Length >= KERB_STORED_CREDENTIAL.StructSize)
											{
												try
												{
													ParseKerberosCreds2<KERB_STORED_CREDENTIAL, KERB_KEY_DATA>(creds, propValueBytes, context.options);
												}
												catch { }
											}
										}
										break;
									case "Primary:Kerberos-Newer-Keys":
										if (0 != (context.options & (CredentialExtractOptions.IncludeKerberos | CredentialExtractOptions.IncludeKerberosOld)))
										{
											if (propValueBytes.Length >= KERB_STORED_CREDENTIAL.StructSize)
											{
												try
												{
													ParseKerberosCreds2<KERB_STORED_CREDENTIAL_NEW, KERB_KEY_DATA_NEW>(creds, propValueBytes, context.options);
												}
												catch { }
											}
										}
										break;
									case "Packages":
										break;
									default:
										break;
								}

								propBytes = propBytes.Slice(offset);
							}
							catch (Exception ex)
							{
								// TODO: Log error
							}
						}
					}
				}
			}
		}
	}

	private static void ExtractHistory(
		IDirectoryObject item,
		byte[] historyBytes,
		uint rid,
		ReadOnlySpan<byte> keyListBytes, CredentialType credType,
		List<Credential> creds)
	{
		historyBytes = DecryptSecret(historyBytes, keyListBytes);
		const int HashSize = 16;

		int hashCount = historyBytes.Length / HashSize;
		for (int i = 0; i < hashCount; i++)
		{
			var hash = historyBytes.AsSpan().Slice(i * HashSize, HashSize);
			var cred = TryDecryptUserHash(hash, rid, credType | (CredentialType)i);
			creds.Add(cred);
		}
	}

	interface IKerbCreds<TKeyData>
		where TKeyData : struct, IKerbKeyData
	{
		int StructSize { get; }
		int KeyDataStructSize { get; }

		int DefaultSaltOffset { get; }
		int DefaultSaltLength { get; }
		int OldCredentialCount { get; }
		int CredentialCount { get; }
	}

	interface IKerbKeyData
	{
		int KeyOffset { get; }
		int KeyLength { get; }
		KerberosKeyType KeyType { get; }
	}

	private static void ParseKerberosCreds2<TKerbCreds, TKeyData>(
		List<Credential> creds,
		byte[]? propValueBytes,
		CredentialExtractOptions options
		)
		where TKerbCreds : struct, IKerbCreds<TKeyData>
		where TKeyData : struct, IKerbKeyData
	{
		var includeOld = 0 != (options & CredentialExtractOptions.IncludeKerberosOld);
		var includeNew = 0 != (options & CredentialExtractOptions.IncludeKerberos);

		ref TKerbCreds kerb = ref MemoryMarshal.AsRef<TKerbCreds>(propValueBytes);
		var salt = propValueBytes.AsSpan().Slice(kerb.DefaultSaltOffset, kerb.DefaultSaltLength);
		{
			var cred = new CleartextCredential(CredentialType.KerberosSalt, Encoding.Unicode.GetString(salt));
			creds.Add(cred);
		}

		var credCount = kerb.OldCredentialCount + kerb.CredentialCount;

		var keyDataSpan = MemoryMarshal.Cast<byte, TKeyData>(
			propValueBytes.AsSpan().Slice(kerb.StructSize, kerb.KeyDataStructSize * credCount)
			);
		for (int j = 0; j < credCount; j++)
		{
			ref var keyData = ref keyDataSpan[j];
			var keyBytes = propValueBytes.AsSpan().Slice(keyData.KeyOffset, keyData.KeyLength);
			var credType = KerbKeyTypeToCredType(keyData.KeyType);
			bool isNew = (j < kerb.CredentialCount);
			if ((isNew && includeNew) || (!isNew && includeOld))
			{
				if (!isNew)
					credType |= CredentialType.KerberosOld;
				creds.Add(new HashCredential(credType, keyBytes.ToArray()));
			}
		}
	}
	private static void ParseKerberosCreds(List<Credential> creds, byte[]? propValueBytes)
	{
		ref KERB_STORED_CREDENTIAL kerb = ref MemoryMarshal.AsRef<KERB_STORED_CREDENTIAL>(propValueBytes);
		var salt = propValueBytes.AsSpan().Slice(kerb.defaultSaltOffset, kerb.defaultSaltLength);
		{
			var cred = new CleartextCredential(CredentialType.KerberosSalt, Encoding.Unicode.GetString(salt));
			creds.Add(cred);
		}

		var credCount = kerb.oldCredentialCount + kerb.credentialCount;

		var keyDataSpan = MemoryMarshal.Cast<byte, KERB_KEY_DATA>(
			propValueBytes.AsSpan().Slice(KERB_STORED_CREDENTIAL.StructSize, KERB_KEY_DATA.StructSize * credCount)
			);
		for (int j = 0; j < credCount; j++)
		{
			ref var keyData = ref keyDataSpan[j];
			var keyBytes = propValueBytes.AsSpan().Slice(keyData.keyOffset, keyData.keyLength);
			var credType = KerbKeyTypeToCredType(keyData.keyType);
			if (j >= kerb.credentialCount)
				credType |= CredentialType.KerberosOld;
			creds.Add(new HashCredential(credType, keyBytes.ToArray()));
		}
	}

	private static void ParseWDigestCreds(List<Credential> creds, byte[] propValueBytes)
	{
		if (propValueBytes.Length >= WDIGEST_CREDENTIALS.StructSize)
		{
			ref var wdigest = ref MemoryMarshal.AsRef<WDIGEST_CREDENTIALS>(propValueBytes);
			var hashBytes = propValueBytes.AsSpan().Slice(WDIGEST_CREDENTIALS.StructSize);
			if (wdigest.version == 1)
			{
				// According to [MS-SAMR], this should always be 29, but just in case
				for (int j = 0; j < wdigest.hashCount; j++)
				{
					if (propValueBytes.Length < WDIGEST_CREDENTIALS.WdigestHashSize)
						break;

					var hash = hashBytes.Slice(0, WDIGEST_CREDENTIALS.WdigestHashSize);
					var cred = new HashCredential(CredentialType.WDigestGroup | (CredentialType)j, hash.ToArray());
					creds.Add(cred);

					hashBytes = hashBytes.Slice(WDIGEST_CREDENTIALS.WdigestHashSize);
				}
			}
			else
			{
				// TODO: Error?
			}
		}
		else
		{
			// TODO: Show error?
		}
	}

	private static CleartextCredential ParseCleartextPassword(byte[] propValueBytes)
	{
		string str = Encoding.Unicode.GetString(propValueBytes);
		var cred = new CleartextCredential(CredentialType.Password, str);
		return cred;
	}

	// REF: https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-samr/8263e7ab-aba9-43d2-8a36-3a9cb2dd3dad
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct USER_PROPERTIES
	{
		internal unsafe static int StructSize => sizeof(USER_PROPERTIES);
		internal static ushort ValidPropertySignature = 0x0050;

		internal uint reserved1;
		internal int length;
		internal ushort reserved2;
		internal ushort reserved3;
		internal unsafe fixed byte reserved4[96];
		internal ushort propertySignature;
		internal ushort propertyCount;
		// UserPropEntry follows
	}

	// REF: https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-samr/7c0f2eca-1783-450b-b5a0-754cf11f22c9
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct USER_PROPERTY
	{
		internal unsafe static int StructSize => sizeof(USER_PROPERTY);

		internal ushort nameLength;
		internal ushort valueLength;
		internal ushort reserved;
		// Name and value follow
	}

	// REF: https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-samr/830b3962-3a00-42cf-94df-c192dcfd4803
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct WDIGEST_CREDENTIALS
	{
		internal const int WdigestHashSize = 16;

		internal unsafe static int StructSize => sizeof(WDIGEST_CREDENTIALS);

		internal ushort reserved1_2;
		internal byte version;
		internal byte hashCount;
		internal unsafe fixed byte reserved3[12];
		// Followed by hashes
	}
	// REF: https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-samr/866f0180-8694-4e14-be7e-dfd91d91694f
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct KERB_STORED_CREDENTIAL : IKerbCreds<KERB_KEY_DATA>
	{
		internal static unsafe int StructSize => sizeof(KERB_STORED_CREDENTIAL);
		int IKerbCreds<KERB_KEY_DATA>.StructSize => StructSize;
		public int KeyDataStructSize => KERB_KEY_DATA.StructSize;


		internal ushort revision;
		internal ushort flags;
		internal ushort credentialCount;
		public int CredentialCount => this.credentialCount;
		internal ushort oldCredentialCount;
		public int OldCredentialCount => this.oldCredentialCount;
		internal ushort defaultSaltLength;
		public int DefaultSaltLength => this.defaultSaltLength;
		internal ushort defaultSaltMaxLength;
		internal int defaultSaltOffset;
		public int DefaultSaltOffset => this.defaultSaltOffset;
		// Credentials[]
		// OldCredentials[]
		// DefaultSalt
		// KeyValues
	}
	// REF: https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-samr/08cb3ca7-954b-45e3-902e-77512fe3ba8e
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct KERB_STORED_CREDENTIAL_NEW : IKerbCreds<KERB_KEY_DATA_NEW>
	{
		internal static unsafe int StructSize => sizeof(KERB_STORED_CREDENTIAL_NEW);
		int IKerbCreds<KERB_KEY_DATA_NEW>.StructSize => StructSize;
		public int KeyDataStructSize => KERB_KEY_DATA_NEW.StructSize;

		internal ushort revision;
		internal ushort flags;
		internal ushort credentialCount;
		public int CredentialCount => this.credentialCount + this.serviceCredentialCount;
		internal ushort serviceCredentialCount;
		internal ushort oldCredentialCount;
		public int OldCredentialCount => this.oldCredentialCount + this.olderCredentialCount;
		internal ushort olderCredentialCount;
		internal ushort defaultSaltLength;
		public int DefaultSaltLength => this.defaultSaltLength;
		internal ushort defaultSaltMaxLength;
		internal int defaultSaltOffset;
		public int DefaultSaltOffset => this.defaultSaltOffset;
		internal int defaultIterationCount;
		// Credentials[]
		// OldCredentials[]
		// DefaultSalt
		// KeyValues
	}
	// REF: https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-samr/9921c3ed-f470-4f36-9edf-7afe24c468ec
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct KERB_KEY_DATA : IKerbKeyData
	{
		internal static unsafe int StructSize => sizeof(KERB_KEY_DATA);




		internal ushort reserved1;
		internal ushort reserved2;
		internal uint reserved3;
		internal KerberosKeyType keyType;
		public KerberosKeyType KeyType => this.keyType;
		internal int keyLength;
		public int KeyLength => this.keyLength;
		internal int keyOffset;
		public int KeyOffset => this.keyOffset;
	}
	// REF: https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-samr/447520a5-e1cc-48cc-8fdc-b90db57f7eac
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct KERB_KEY_DATA_NEW : IKerbKeyData
	{
		internal static unsafe int StructSize => sizeof(KERB_KEY_DATA_NEW);

		internal ushort reserved1;
		internal ushort reserved2;
		internal uint reserved3;
		internal int iterationCount;
		internal KerberosKeyType keyType;
		public KerberosKeyType KeyType => this.keyType;
		internal int keyLength;
		public int KeyLength => this.keyLength;
		internal int keyOffset;
		public int KeyOffset => this.keyOffset;
	}

	static CredentialType KerbKeyTypeToCredType(KerberosKeyType kerbType)
		=> kerbType switch
		{
			KerberosKeyType.DecCbcCrc => CredentialType.KerberosDecCbcCrc,
			KerberosKeyType.DesCbcMd5 => CredentialType.KerberosDesCbcMd5,
			KerberosKeyType.Aes128CtsHmacSha1_96 => CredentialType.KerberosAes128CtsHmacSha1_96,
			KerberosKeyType.Aes256CtsHmacSha1_96 => CredentialType.KerberosAes256CtsHmacSha1_96,
			_ => CredentialType.Unspecified,
		};

	// REF: https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-samr/9921c3ed-f470-4f36-9edf-7afe24c468ec
	public enum KerberosKeyType : uint
	{
		DecCbcCrc = 1,
		DesCbcMd5 = 3,
		Aes128CtsHmacSha1_96 = 17,
		Aes256CtsHmacSha1_96 = 18,
	}

	private static Credential TryDecryptUserSecret(
		IDirectoryObject obj,
		uint rid,
		Span<byte> secretBytes,
		ReadOnlySpan<byte> keyListBytes,
		CredentialType credentialType)
	{
		try
		{
			var decryptedSecret = DecryptSecret(secretBytes, keyListBytes);
			Debug.Assert(decryptedSecret.Length == 16);

			return TryDecryptUserHash(decryptedSecret, rid, credentialType);
		}
		catch (Exception ex)
		{
			return new HashCredential(credentialType, ex.Message);
		}
	}

	private static Credential TryDecryptUserHash(
		ReadOnlySpan<byte> secret,
		uint rid,
		CredentialType credentialType)
	{
		Span<uint> desKeys = stackalloc uint[4];
		desKeys[0] = rid;
		desKeys[1] = rid;
		desKeys[2] = rid;
		desKeys[3] = rid;

		Span<byte> desKeyBytes = MemoryMarshal.AsBytes(desKeys);

		var key = new byte[8];
		UnpackDesKey(desKeyBytes.Slice(0, 7), key);

		byte[] decryptedPassword = new byte[16];

		var des = DES.Create();
		des.Key = key;
		des.DecryptEcb(secret.Slice(0, 8), new Span<byte>(decryptedPassword, 0, 8), PaddingMode.None);
		UnpackDesKey(desKeyBytes.Slice(7, 7), key);
		des.Key = key;
		des.DecryptEcb(secret.Slice(8, 8), new Span<byte>(decryptedPassword, 8, 8), PaddingMode.None);

		return new HashCredential(credentialType, decryptedPassword);
	}

	private static void UnpackDesKey(Span<byte> packedKeyBytes, Span<byte> unpackedKeyBytes)
	{
		Debug.Assert(packedKeyBytes.Length == 7);
		Debug.Assert(unpackedKeyBytes.Length == 8);
		Debug.Assert(BitConverter.IsLittleEndian);

		// Break the 56-bit key into 8 7-bit chunks and
		// place into the 64-bit output buffer, aligned to the LSB
		// This implementation doesn't clear the MSB, which is done below
		unpackedKeyBytes[0] = (byte)(packedKeyBytes[0] >> 0x01);
		unpackedKeyBytes[1] = (byte)((packedKeyBytes[0] << 6) | (packedKeyBytes[1] >> 2));
		unpackedKeyBytes[2] = (byte)((packedKeyBytes[1] << 5) | (packedKeyBytes[2] >> 3));
		unpackedKeyBytes[3] = (byte)((packedKeyBytes[2] << 4) | (packedKeyBytes[3] >> 4));
		unpackedKeyBytes[4] = (byte)((packedKeyBytes[3] << 3) | (packedKeyBytes[4] >> 5));
		unpackedKeyBytes[5] = (byte)((packedKeyBytes[4] << 2) | (packedKeyBytes[5] >> 6));
		unpackedKeyBytes[6] = (byte)((packedKeyBytes[5] << 1) | (packedKeyBytes[6] >> 7));
		unpackedKeyBytes[7] = packedKeyBytes[6];

		// Shift the bits to the left
		ref var unpackedKey = ref MemoryMarshal.AsRef<ulong>(unpackedKeyBytes);
		unpackedKey <<= 1;

		// Clear the parity bits
		unpackedKey &= 0xfefefefe_fefefefe;
	}

	private static byte[] DecryptSecret(
		ReadOnlySpan<byte> secretBytes,
		ReadOnlySpan<byte> keyListBytes
		)
	{
		if (secretBytes.Length == 0)
			throw new ArgumentNullException(nameof(secretBytes));
		else if (secretBytes.Length < SecretHeader.StructSize)
			throw new ArgumentException("The array is too small to contain a valid secret.", nameof(secretBytes));

		ref readonly SecretHeader secret = ref MemoryMarshal.AsRef<SecretHeader>(secretBytes);
		if (secret.version != SecretHeader.Win2016SecretVersion)
			throw new NotSupportedException($"The secret is encoded with version {secret.version}, which is not supported by this implementation.");

		var keyOffset = secret.keyIndex * CredentialExtraction.PekList.KeySize;
		if ((uint)(keyOffset + CredentialExtraction.PekList.KeySize) > (uint)keyListBytes.Length)
			throw new InvalidDataException($"The secret references key index {secret.keyIndex}, which is not in the key list.");

		var salt = secret.salt.GetBytes();
		var data = secretBytes.Slice(SecretHeader.StructSize);
		if (data.Length < secret.cbData)
			throw new InvalidDataException($"The secret appears to be incomplete.  The reported size is {secret.cbData}, but only {data.Length} bytes are available.");

		var key = keyListBytes.Slice(keyOffset, CredentialExtraction.PekList.KeySize);
		Aes aes = Aes.Create();
		aes.Key = key.ToArray();

		var decrypted = aes.DecryptCbc(data, salt, PaddingMode.PKCS7);
		return decrypted;
	}

	private static bool CompareBytes(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y)
	{
		if (x.Length != y.Length)
			throw new ArgumentException("The spans are not the same size.", nameof(y));
		for (int i = 0; i < x.Length; i++)
		{
			if (x[i] != y[i])
				return false;
		}
		return true;
	}

	private bool CanExtract()
		=> _systemKeyBytes != null && _systemKeyBytes.Length == 16 && this._options != 0;



	private static int _lastFilterIndex = 1;
	private bool CanExport()
		=> (this.Credentials != null && this.Credentials.Count > 0);
	private void Export()
	{
		SaveFileDialog dl = new SaveFileDialog()
		{
			Title = Messages.Export_Title,
			Filter = "Tab-delimited text (*.txt)|*.txt|Comma-separated values (*.csv)|*.csv|Password dump file (*.txt)|*.txt",
			FileName = "credentials.txt",
			FilterIndex = _lastFilterIndex
		};


		var res = dl.ShowDialog(Window);
		if (res ?? false)
		{
			_lastFilterIndex = dl.FilterIndex;

			var creds = this.Credentials;
			string fileName = dl.FileName;
			try
			{
				int recordCount = 0;
				using (StreamWriter writer = File.CreateText(fileName))
				{
					if (dl.FilterIndex == 3)
					{
						ExportPwdump(creds, writer);
					}
					else
					{
						TabularTextWriter builder = dl.FilterIndex switch
						{
							1 => new TsvBuilder(writer),
							_ => new CsvBuilder(writer)
						};

						foreach (var cred in creds)
						{
							if (cred.Text != null)
							{
								recordCount++;
								builder.WriteValue(cred.LogonName);
								builder.WriteValue(cred.Label);
								builder.WriteValue(cred.Text);
								builder.WriteValue(cred.Upn);
								builder.WriteValue(cred.Node.Object.DistinguishedName);
								builder.EndRecord();
							}
						}
					}
				}

				this.ReportExportSuccess(fileName, recordCount, Messages.Export_Title);
			}
			catch (Exception ex)
			{
				ReportError("Unable to export credentials to a file: " + ex.Message, Messages.Export_Title, ex);
			}
		}

	}

	private void ExportPwdump(IList<CredentialItem>? creds, StreamWriter writer)
	{
		var attrSid = this.Directory.TryGetAttributeByLdapName(CredentialAttributeNames.ObjectSid);
		var credGroups = creds.GroupBy(r => r.LogonName);
		foreach (var credGroup in credGroups)
		{
			// Handle NT/LM hashes
			{
				var lmcred = credGroup.FirstOrDefault(r => r.CredentialType == CredentialType.LmHash);
				var ntcred = credGroup.FirstOrDefault(r => r.CredentialType == CredentialType.NtHash);
				if (lmcred is not null || ntcred is not null)
				{
					var obj = credGroup.First();
					var sid = obj.Node.Object.GetValueOf(attrSid) as SecurityIdentifier;
					var rid = sid.GetRid();

					var lmhash = lmcred?.Text ?? HashCredential.MissingLmHash;
					var nthash = ntcred?.Text ?? HashCredential.MissingNtHash;
					writer.WriteLine($"{credGroup.Key}:{rid}:{lmhash}:{nthash}:::");
				}
			}

			foreach (var cred in credGroup)
			{
				var label = cred.PwdumpLabel;
				if (label is not null)
					writer.WriteLine($"{credGroup.Key}:{label}:{cred.Text}::::");
			}

			// NT/LM history
			{
				var lmhist = credGroup.Where(r => (r.CredentialType & CredentialType.GroupMask) is CredentialType.LmHistory);
				var nthist = credGroup.Where(r => (r.CredentialType & CredentialType.GroupMask) is CredentialType.NtHistory);
				var ntlmhist = nthist.FullOuterHashJoin(
					lmhist,
					r => (int)(r.CredentialType & CredentialType.IndexMask),
					r => (int)(r.CredentialType & CredentialType.IndexMask),
					(nt, lm) => new { nt, lm });
				foreach (var item in ntlmhist)
				{
					var obj = item.nt;
					var sid = obj.Node.Object.GetValueOf(attrSid) as SecurityIdentifier;
					var rid = sid.GetRid();

					var index = (int)((item.nt?.CredentialType ?? item.lm?.CredentialType).Value & CredentialType.IndexMask);
					var lmhash = item.lm?.Text ?? HashCredential.MissingLmHash;
					var nthash = item.nt?.Text ?? HashCredential.MissingNtHash;
					writer.WriteLine($"{credGroup.Key}-hist{index}:{rid}:{lmhash}:{nthash}:::");
				}
			}
		}
	}
}
