using DitExplorer.EseInterop;
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
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace DitExplorer.CredentialExtraction;

internal class CredentialExtractorViewModel : ViewModel
{
	public static ICommand ExtractCommand = new RoutedCommand("Tools.ExtractCredentials", typeof(CredentialExtractorViewModel));
	public static ICommand ExportCommand = new RoutedCommand("Tools.Export", typeof(CredentialExtractorViewModel));

	public CredentialExtractorViewModel(IDirectoryNode[] nodes)
	{
		if (nodes == null || nodes.Length == 0)
			throw new ArgumentException(nameof(nodes));

		Nodes = nodes;
		this.Directory = nodes[0].DirectoryView.Directory;

		this.RegisterCommand(ExtractCommand, Extract, CanExtract);
		this.RegisterCommand(ExportCommand, Export, CanExport);
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


	private static int ParseHexChar(char c)
	{
		if ((uint)(c - '0') < 10)
			return c - '0';
		else if ((uint)(c - 'a') <= 'f' - 'a')
			return c - 'a' + 10;
		else if ((uint)(c - 'A') <= 'F' - 'A')
			return c - 'A' + 10;
		else
			return -1;
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

			byte[] bytes = new byte[16];
			for (int i = 0; i < value.Length; i += 2)
			{
				int d1 = ParseHexChar(value[i]);
				int d2 = ParseHexChar(value[i + 1]);
				if (d1 < 0 || d2 < 0)
				{
					SystemKeyError = $"The string contains an invalid digit at position {i}";
					return null;
				}
				else
					bytes[i / 2] = (byte)((d1 << 4) | d2);
			}
			SystemKeyError = null;
			return bytes;
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

	private IList<Credential>? _creds;

	public IList<Credential>? Credentials
	{
		get { return _creds; }
		set => this.NotifyIfChanged(ref _creds, value);
	}


	private void Extract()
	{
		var systemKey = _systemKeyBytes;
		if (systemKey == null)
			return;

		var dir = Nodes[0].Object.Directory;

		var items = this.Nodes.Select(r => r.Object).ToArray();
		var creds = ExtractCredentials(systemKey, dir, items);
		this.Credentials = creds;
	}

	private static readonly byte[] PekListAuthenticator = new byte[]{
		0x56, 0xD9, 0x81, 0x48, 0xEC, 0x91, 0xD1, 0x11,
		0x90, 0x5A, 0x00, 0xC0, 0x4F, 0xC2, 0xD4, 0xCF
	};

	private static IList<Credential> ExtractCredentials(byte[] systemKey, IDirectory dir, IDirectoryObject[] items)
	{
		if (systemKey == null || systemKey.Length == 0)
			throw new ArgumentNullException(nameof(systemKey));
		if (systemKey.Length != 16)
			throw new ArgumentException("The system key is not the correct size.  It must be exactly 16 bytes in size.", nameof(systemKey));

		var attrDbcsPwd = dir.TryGetAttributeByLdapName("dBCSPwd");
		var attrUnicodePwd = dir.TryGetAttributeByLdapName("unicodePwd");
		var attrSupplementalCredentials = dir.TryGetAttributeByLdapName("supplementalCredentials");
		var attrSid = dir.TryGetAttributeByLdapName("objectSid");

		var domain = dir.RootDomain;
		var attrPekList = dir.TryGetAttributeByLdapName("pekList");
		var pekListBytes = domain.GetValueOf(attrPekList) as byte[];
		if (pekListBytes == null)
			throw new Exception("Unable to retrieve the pekList attribute from the domain.");

		pekListBytes = DecryptPekList(pekListBytes, systemKey);
		if (!CompareBytes(new ReadOnlySpan<byte>(pekListBytes, 0, PekListAuthenticator.Length), PekListAuthenticator))
			throw new InvalidDataException("Unable to verify the pekList with the key provided.  This most likely indicates that the system key is incorrect.");

		ref var pekData = ref MemoryMarshal.AsRef<PekEncryptedHeader>(pekListBytes);
		var keyListBytes = new ReadOnlySpan<byte>(pekListBytes).Slice(PekEncryptedHeader.StructSize);

		List<Credential> creds = new List<Credential>(items.Length * 2);
		byte[] sidBytes = new byte[40];
		foreach (var item in items)
		{
			var sid = item.GetValueOf(attrSid) as SecurityIdentifier;
			if (sid != null)
			{
				if (sid.BinaryLength > sidBytes.Length)
					sidBytes = new byte[sid.BinaryLength];
				sid.GetBinaryForm(sidBytes, 0);
				var rid = BinaryPrimitives.ReadUInt32LittleEndian(sidBytes[(sid.BinaryLength - 4)..sid.BinaryLength]);

				var dbcsPassword = item.GetValueOf(attrDbcsPwd) as byte[];
				if (dbcsPassword != null)
				{
					var cred = TryDecryptUserSecret(item, rid, dbcsPassword, keyListBytes, CredentialType.LmHash);
					creds.Add(cred);
				}

				var unicodePassword = item.GetValueOf(attrUnicodePwd) as byte[];
				if (unicodePassword != null)
				{
					var cred = TryDecryptUserSecret(item, rid, unicodePassword, keyListBytes, CredentialType.NtHash);
					creds.Add(cred);
				}
			}
		}

		return creds;
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

			Span<uint> desKeys = stackalloc uint[4];
			desKeys[0] = rid;
			desKeys[1] = rid;
			desKeys[2] = rid;
			desKeys[3] = rid;

			Span<byte> desKeyBytes = MemoryMarshal.AsBytes(desKeys);

			var key = new byte[8];
			UnpackDesKey(desKeyBytes.Slice(0, 7), key);

			byte[] decryptedPassword = new byte[16];

			var des = DESCryptoServiceProvider.Create();
			des.Key = key;
			des.DecryptEcb(new ReadOnlySpan<byte>(decryptedSecret, 0, 8), new Span<byte>(decryptedPassword, 0, 8), PaddingMode.None);
			UnpackDesKey(desKeyBytes.Slice(7, 7), key);
			des.Key = key;
			des.DecryptEcb(new ReadOnlySpan<byte>(decryptedSecret, 8, 8), new Span<byte>(decryptedPassword, 8, 8), PaddingMode.None);

			return new HashCredential(obj, credentialType, decryptedPassword);
		}
		catch (Exception ex)
		{
			return new HashCredential(obj, credentialType, ex.Message);
		}
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

		var keyOffset = secret.keyIndex * PekList.KeySize;
		if ((uint)(keyOffset + PekList.KeySize) > (uint)keyListBytes.Length)
			throw new InvalidDataException($"The secret references key index {secret.keyIndex}, which is not in the key list.");

		var salt = secret.salt.GetBytes();
		var data = secretBytes.Slice(SecretHeader.StructSize);
		if (data.Length < secret.cbData)
			throw new InvalidDataException($"The secret appears to be incomplete.  The reported size is {secret.cbData}, but only {data.Length} bytes are available.");

		var key = keyListBytes.Slice(keyOffset, PekList.KeySize);
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
		=> _systemKeyBytes != null && _systemKeyBytes.Length == 16;



	private bool CanExport()
		=> (this.Credentials != null && this.Credentials.Count > 0);
	private void Export()
	{
		SaveFileDialog dl = new SaveFileDialog()
		{
			Title = Messages.Export_Title,
			Filter = "Password dump file (*.txt)|*.txt|Tab-delimited text (*.txt)|*.txt|Comma-separated values (*.csv)|*.csv",
			FileName = "credentials.txt"
		};


		const string MissingLmHash = "aad3b435b51404eeaad3b435b51404ee";
		const string MissingNtHash = "31d6cfe0d16ae931b73c59d7e0c089c0";
		var res = dl.ShowDialog(Window);
		if (res ?? false)
		{
			var creds = this.Credentials;
			string fileName = dl.FileName;
			try
			{
				int recordCount = 0;
				using (StreamWriter writer = File.CreateText(fileName))
				{
					if (dl.FilterIndex == 1)
					{
						var attrAccountName = this.Directory.TryGetAttributeByLdapName("samAccountName");
						var attrSid = this.Directory.TryGetAttributeByLdapName("objectSid");
						var credGroups = creds.GroupBy(r => r.Object.GetValueOf(attrAccountName) as string);
						foreach (var credGroup in credGroups)
						{
							var lmcred = credGroup.FirstOrDefault(r => r.CredentialType == CredentialType.LmHash);
							var ntcred = credGroup.FirstOrDefault(r => r.CredentialType == CredentialType.NtHash);

							if (lmcred is not null || ntcred is not null)
							{
								var obj = credGroup.First();
								var sid = obj.Object.GetValueOf(attrSid) as SecurityIdentifier;
								var rid = sid.GetRid();

								var lmhash = lmcred?.Text ?? MissingLmHash;
								var nthash = ntcred?.Text ?? MissingNtHash;
								writer.WriteLine($"{credGroup.Key}:{rid}:{lmhash}:{nthash}:::");
							}
						}
					}
					else
					{
						TabularTextWriter builder = dl.FilterIndex switch
						{
							2 => new TsvBuilder(writer),
							_ => new CsvBuilder(writer)
						};

						foreach (var cred in creds)
						{
							if (cred.Text != null)
							{
								recordCount++;
								builder.WriteValue(cred.Object.DistinguishedName);
								builder.WriteValue(cred.Label);
								builder.WriteValue(cred.Text);
								builder.EndRecord();
							}
						}
					}
				}

				_ = MessageBox.Show(Window, $"Finished exporting {recordCount} records to {fileName}.", Messages.Export_Title, MessageBoxButton.OK, MessageBoxImage.Information);
			}
			catch (Exception ex)
			{
				ReportError("Unable to export credentials to a file: " + ex.Message, Messages.Export_Title, ex);
			}
		}

	}
}
