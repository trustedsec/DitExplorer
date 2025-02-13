using Microsoft.Isam.Esent.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.EseInterop;

/// <summary>
/// Specifies the data type of a column in an ESE database.
/// </summary>
/// <remarks>
/// Includes all values from <see href="https://learn.microsoft.com/en-us/windows/win32/extensible-storage-engine/jet-coltyp">the ESE documentation</see>.
/// <see cref="JET_coltp"/> doesn't include all values.
/// </remarks>
public enum JetColumnType
{
	Nil = 0,
	Bit = 1,
	UnsignedByte = 2,
	Short = 3,
	Long = 4,
	Currency = 5,
	Single = 6,
	Double = 7,
	DateTime = 8,
	Binary = 9,
	Text = 10,
	LongBinary = 11,
	LongText = 12,
	SLV = 13,
	UnsignedLong = 14,
	LongLong = 15,
	Guid = 16,
	UnsignedShort = 17,
}
