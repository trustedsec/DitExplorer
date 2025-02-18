namespace DitExplorer.Ntds;

// REF: https://learn.microsoft.com/en-us/windows/win32/adschema/a-searchflags
[Flags]
public enum ADSearchFlags
{
	None = 0,

	Indexed = 1,
	IndexedPerContainer = 2,
	Anr = 4,
	TombstonePreserve = 8,
	CopyWithObject = 0x10,
	TupleIndex = 0x20,
	IndexedVlv = 0x40,
	Confidential = 0x80,
}
