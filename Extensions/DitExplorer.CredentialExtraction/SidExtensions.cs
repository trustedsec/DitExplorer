using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.CredentialExtraction;
internal static class SidExtensions
{
	public static uint GetRid(this SecurityIdentifier sid)
	{
		byte[] sidBytes = new byte[sid.BinaryLength];
		sid.GetBinaryForm(sidBytes, 0);
		var rid = BinaryPrimitives.ReadUInt32LittleEndian(new ReadOnlySpan<byte>(sidBytes).Slice(sid.BinaryLength - 4,4));
		return rid;
	}
}
