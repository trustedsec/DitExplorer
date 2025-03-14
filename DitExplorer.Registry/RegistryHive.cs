
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Formats.Asn1;
using System.Runtime.InteropServices;
using System.Text;

namespace DitExplorer.Registry;

public class RegistryHiveKey
{
	private readonly uint knOffset;

	internal RegistryHiveKey(RegistryHive hive, uint cellOffset)
	{
		Hive = hive;
		this.knOffset = cellOffset;

		var cellBytes = this.Hive.GetCellBytes(this.knOffset);
		// TODO: Ensure this checks the size of the underlying span
		ref readonly var kn = ref MemoryMarshal.AsRef<KeyNodeCell>(cellBytes);
		if (kn.hdr.cellType != CellType.KeyNode)
			throw new InvalidDataException(Messages.RegistryHiveKey_WrongCellType);

		var nameBytes = cellBytes.Slice(KeyNodeCell.StructSize, kn.keyNameLength);

		string name = ((0 != (kn.flags & KeyNodeFlags.AsciiName)) ? Encoding.ASCII : Encoding.Unicode).GetString(nameBytes);
		this.Name = name;

		if (kn.classNameOffset != RegistryHive.UnallocatedOffset)
		{
			var className = Encoding.Unicode.GetString(this.Hive.GetCountedData(kn.classNameOffset).Slice(0, kn.classNameLength));
			this.ClassName = className;
		}
	}

	private ref readonly KeyNodeCell keyNodeCell => ref MemoryMarshal.AsRef<KeyNodeCell>(this.Hive.GetCellBytes(this.knOffset));

	public RegistryHive Hive { get; }
	public string Name { get; }
	public string ClassName { get; }
	public int ValueCount => this.keyNodeCell.keyValueCount;
	public int SubkeyCount => this.keyNodeCell.subkeyCount;
	public int LongestValueNameLength => this.keyNodeCell.longestValueNameLength;
	public int LongestKeyNameLength => this.keyNodeCell.longestSubkeyNameLength;
	public int LongestKeyClassNameLength => this.keyNodeCell.longestSubkeyClassNameLength;
	public int LargestValueDataSize => this.keyNodeCell.longestValueData;

	public ReadOnlySpan<byte> GetValueData(string name)
	{
		if (name == null) name = string.Empty;

		ref readonly KeyNodeCell kn = ref this.keyNodeCell;
		if (kn.keyValueListOffset == RegistryHive.UnallocatedOffset)
			throw new KeyNotFoundException();

		var valueOffsets = MemoryMarshal.Cast<byte, uint>(this.Hive.GetBytes(kn.keyValueListOffset, 4 * kn.keyValueCount));
		// TODO: Binary search
		foreach (var valueOffset in valueOffsets)
		{
			ReadOnlySpan<byte> cellBytes = this.Hive.GetCellBytes(valueOffset);
			ref readonly KeyValueCell valueCell = ref MemoryMarshal.AsRef<KeyValueCell>(cellBytes);
			var nameBytes = cellBytes.Slice(KeyValueCell.StructSize);
			var valueName = ((0 != (valueCell.flags & KeyValueFlags.AsciiName)) ? Encoding.ASCII : Encoding.Unicode).GetString(nameBytes);

			if (valueName.Equals(name, StringComparison.OrdinalIgnoreCase))
			{
				if (valueCell.dataSize < 0)
				{
					return MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan<int>(in valueCell.dataOffset, 1));
				}
				else
				{
					ref readonly var dataCellHdr = ref this.Hive.GetCellHeader((uint)valueCell.dataOffset);
					throw new NotImplementedException();
				}
			}
		}

		throw new KeyNotFoundException();
	}

	private static uint HashName(string name)
	{
		uint hash = 0;
		foreach (var c in name.ToUpper())
		{
			hash = 37 * hash + c;
		}
		return hash;
	}

	public RegistryHiveKey? GetSubkey(string name)
	{
		if (string.IsNullOrEmpty(name)) throw new ArgumentException($"'{nameof(name)}' cannot be null or empty.", nameof(name));

		ref readonly KeyNodeCell kn = ref this.keyNodeCell;
		if (kn.subkeysListOffset == RegistryHive.UnallocatedOffset)
			return null;

		var nameHash = HashName(name);

		var listCellBytes = this.Hive.GetCellBytes(kn.subkeysListOffset);
		ref readonly var subkeyListHdr = ref MemoryMarshal.AsRef<CellHeader>(listCellBytes);
		if (subkeyListHdr.cellType == CellType.HashLeaf)
		{
			ref readonly var hashLeafCell = ref MemoryMarshal.AsRef<HashLeafCell>(listCellBytes);
			var entries = MemoryMarshal.Cast<byte, HashLeafElement>(listCellBytes.Slice(HashLeafCell.StructSize));
			// TODO: Binary search
			foreach (var entry in entries)
			{
				if (entry.nameHash == nameHash)
				{
					RegistryHiveKey subkey = this.Hive.OpenKey(entry.keyNodeOffset);
					if (subkey.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
						return subkey;
				}
			}

			throw new KeyNotFoundException();
		}
		throw new NotImplementedException();
	}
}

/// <summary>
/// Provides a view of a registry hive file.
/// </summary>
public class RegistryHive
{
	internal readonly byte[] bytes;

	internal const int BinStartOffset = 4096;
	internal const uint UnallocatedOffset = 0xFFFFFFFF;

	/// <summary>
	/// Initializes a new <see cref="RegistryHive"/>.
	/// </summary>
	/// <param name="bytes"></param>
	public RegistryHive(byte[] bytes)
	{
		if (bytes is null) throw new ArgumentNullException(nameof(bytes));

		if (bytes.Length < BaseBlock.StructSize)
			throw new InvalidDataException(Messages.RegistryHive_NotHive);

		this.bytes = bytes;

		ref BaseBlock baseBlock = ref this.BaseBlock;
		if (baseBlock.signature == BaseBlock.StructSize)
			throw new InvalidDataException(Messages.RegistryHive_NotHive);

		this.RootKey = this.OpenKey(baseBlock.rootCellOffset);
	}

	private ref BaseBlock BaseBlock => ref MemoryMarshal.AsRef<BaseBlock>(this.bytes);

	internal RegistryHiveKey OpenKey(uint keyNodeOffset)
	{
		return new RegistryHiveKey(this, keyNodeOffset);
	}

	public string HiveName => this.BaseBlock.FileName;

	public RegistryHiveKey RootKey { get; }

	internal ReadOnlySpan<byte> GetCellBytes(uint offset)
	{
		ref readonly var cell = ref this.GetCellHeader(offset);
		if (cell.AllocatedSize == 0)
			throw new InvalidOperationException("The requested cell is not allocated.");
		var cellBytes = this.bytes.AsSpan().Slice((int)offset + BinStartOffset, cell.AllocatedSize);
		return cellBytes;
	}

	internal ReadOnlySpan<byte> GetBytes(uint offset, int length)
	{
		if (offset == UnallocatedOffset)
			throw new InvalidAsynchronousStateException("The cell is not allocated");

		var bytes = this.bytes.AsSpan().Slice((int)offset + BinStartOffset, length);
		return bytes;
	}

	internal ReadOnlySpan<byte> GetCountedData(uint offset)
	{
		if (offset == UnallocatedOffset)
			throw new InvalidAsynchronousStateException("The cell is not allocated");
		int cbData = MemoryMarshal.Read<int>(this.bytes.AsSpan().Slice((int)offset + BinStartOffset));
		if (cbData < 0)
		{
			cbData = -cbData;
			var bytes = this.bytes.AsSpan().Slice((int)offset + BinStartOffset + 4, cbData - 4);
			return bytes;
		}
		else
		{
			return new ReadOnlySpan<byte>();
		}
	}

	internal ref readonly CellHeader GetCellHeader(uint offset)
	{
		if (offset == UnallocatedOffset)
			throw new InvalidAsynchronousStateException("The cell is not allocated");
		ref readonly var cell = ref MemoryMarshal.AsRef<CellHeader>(this.bytes.AsSpan().Slice((int)offset + BinStartOffset));
		return ref cell;
	}
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct BaseBlock
{
	public static unsafe int StructSize => sizeof(BaseBlock);

	internal const uint ValidSignature =
		'r'
		| ('e' << 8)
		| ('g' << 16)
		| ('f' << 24)
		;

	internal uint signature;
	internal int primarySeq;
	internal int secondarySeq;
	internal long writeTimestamp;
	internal int majorVersion;
	internal int minorVersion;
	internal int fileType;
	internal int fileFormat;
	internal uint rootCellOffset;
	internal int hiveBinsDataSize;
	internal int clusteringFactor;
	const int FileNameLength = 32;
	internal unsafe fixed char fileName[FileNameLength];
	public string FileName
	{
		get
		{
			unsafe
			{
				fixed (char* pFileName = this.fileName)
				{
					int i;
					for (i = 0; i < FileNameLength && pFileName[i] != '\0'; i++)
						;
					return new string(pFileName, 0, i);
				}
			}
		}
	}

	internal unsafe fixed byte reserved[396];

	internal int checksum;
	internal unsafe fixed byte reserved2[3576];
	internal int bootType;
	internal int bootRecover;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct HiveBinHeader
{
	public static unsafe int StructSize => sizeof(HiveBinHeader);
	internal const uint ValidSignature =
		'h'
		| ('b' << 8)
		| ('i' << 16)
		| ('n' << 24)
		;

	internal uint signature;
	internal uint offset;
	internal uint size;
	internal ulong reserved;
	internal ulong timestamp;
	internal uint spare;
}

enum CellType : ushort
{
	IndexLeaf = ('i' << 8) | 'l',
	FastLeaf = ('f' << 8) | 'l',
	HashLeaf = ('h' << 8) | 'l',
	IndexRoot = ('i' << 8) | 'r',
	KeyNode = ('k' << 8) | 'n',
	KeyValue = ('k' << 8) | 'v',
	KeySecurity = ('k' << 8) | 's',
	BigData = ('b' << 8) | 'd',
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct CellHeader
{
	public static unsafe int StructSize => sizeof(CellHeader);

	private int size;
	internal int AllocatedSize => (this.size < 0) ? -this.size : 0;
	internal CellType cellType;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct IndexLeafCell
{
	public static unsafe int StructSize => sizeof(IndexLeafCell);

	internal CellHeader hdr;
	internal ushort cElements;
	internal IndexLeafElement elements;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct IndexLeafElement
{
	public static unsafe int StructSize => sizeof(IndexLeafElement);

	internal int keyNodeOffset;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct FastLeafCell
{
	public static unsafe int StructSize => sizeof(FastLeafCell);

	internal CellHeader hdr;
	internal ushort cElements;
	internal FastLeafElement elements;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct FastLeafElement
{
	public static unsafe int StructSize => sizeof(FastLeafElement);

	internal int keyNodeOffset;
	internal uint nameHint;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct HashLeafCell
{
	public static unsafe int StructSize => sizeof(HashLeafCell);

	internal CellHeader hdr;
	internal ushort cElements;
	//internal HashLeafElement element0;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct HashLeafElement
{
	public static unsafe int StructSize => sizeof(HashLeafElement);

	internal uint keyNodeOffset;
	internal uint nameHash;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct IndexRootCell
{
	public static unsafe int StructSize => sizeof(IndexRootCell);

	internal CellHeader hdr;
	internal ushort cElements;
	internal IndexLeafElement elements;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct IndexRootElement
{
	public static unsafe int StructSize => sizeof(IndexRootElement);

	internal int subkeyListOffset;
}

[Flags]
enum KeyNodeFlags : ushort
{
	None = 0,
	IsVolatile = 1,
	MountPoint = 2,
	IsRoot = 4,
	CantDelete = 8,
	Symlink = 0x10,
	AsciiName = 0x20,
	PredefHandle = 0x40,
	VirtualSource = 0x80,
	VirtualTarget = 0x100,
	VirtualStore = 0x200,
}

[Flags]
enum KeyNodeFlags2 : byte
{

}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct KeyNodeCell
{
	public static unsafe int StructSize => sizeof(KeyNodeCell);

	internal CellHeader hdr;
	internal KeyNodeFlags flags;
	internal long lastWriteTime;
	internal uint accessBits;
	internal uint parentKeyNodeOffset;
	internal int subkeyCount;
	internal int volatileSubkeyCount;
	internal uint subkeysListOffset;
	internal uint volatileSubkeysListOffset;
	internal int keyValueCount;
	internal uint keyValueListOffset;
	internal uint keySecurityOffset;
	internal uint classNameOffset;
	internal ushort longestSubkeyNameLength;
	internal KeyNodeFlags2 flags2;
	internal byte debug;
	internal int longestSubkeyClassNameLength;
	internal int longestValueNameLength;
	internal int longestValueData;
	internal int reserved;
	internal ushort keyNameLength;
	internal ushort classNameLength;
	//internal byte keyName0;
}

enum KeyValueDataType : int
{
	None = 0,
	String = 1,
	ExpandableString = 2,
	Binary = 3,
	DwordLE = 4,
	DwordBE = 5,
	Link = 6,
	MultiString = 7,
	ResourceList = 8,
	FullResourceDescriptor = 9,
	ResourceRequirementsList = 0x0A,
	QwordLE = 0x0B,
}

[Flags]
enum KeyValueFlags : ushort
{
	AsciiName = 1,
	IsTombstone = 2,
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct KeyValueCell
{
	public static unsafe int StructSize => sizeof(KeyValueCell);

	internal CellHeader hdr;
	internal ushort nameLength;
	internal int dataSize;
	internal int dataOffset;
	internal KeyValueDataType dataType;
	internal KeyValueFlags flags;
	internal ushort spare;
	// byte name0;
}