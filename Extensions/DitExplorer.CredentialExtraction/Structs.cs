using DitExplorer.CredentialExtraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.CredentialExtraction
{
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct Salt
	{
		const int SaltSize = 16;
		internal unsafe fixed byte salt[SaltSize];
		public Span<byte> GetBytes()
		{
			unsafe
			{
				fixed (byte* pSalt = this.salt)
				{
					return new Span<byte>(pSalt, SaltSize);
				}
			}
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct PekHeader
	{
		internal static unsafe int StructSize => sizeof(PekHeader);

		internal int versionMajor;
		internal int versionMinor;
		internal Salt salt;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct FILETIME
	{
		int lo;
		int hi;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct PekEncryptedHeader
	{
		internal static unsafe int StructSize => sizeof(PekEncryptedHeader);

		internal Salt auth;
		internal FILETIME ftModified;
		internal int unk1;
		internal int keyCount;
		internal int unk2;

		// Followed by a list of keys
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct PekList
	{
		internal static unsafe int StructSize => sizeof(PekList);
		internal const int KeySize = 16;
		internal const int SaltSize = 16;

		internal PekHeader hdr;
		internal PekEncryptedHeader encHdr;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct SecretHeader
	{
		internal static unsafe int StructSize => sizeof(SecretHeader);

		internal const short Win2016SecretVersion = 0x13;

		internal short version;
		internal short unk1;
		internal int keyIndex;
		internal Salt salt;

		internal int cbData;
	}
}