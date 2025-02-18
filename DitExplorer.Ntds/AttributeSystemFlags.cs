using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.Ntds;

// REF: https://learn.microsoft.com/en-us/windows/win32/adschema/a-systemflags
[Flags]
public enum AttributeSystemFlags : uint
{
	None = 0,

	NotReplicated = 1,
	ReplicatedToGC = 2,
	Constructed = 4,
	BaseSchema = 0x10,
	DeletedImmediately = 0x02000000,
	Unmovable = 0x04000000,
	Unrenamable = 0x08000000,
	ConfCanMoveWithRestrictions = 0x10000000,
	ConfCanMove = 0x20000000,
	ConfCanRenameWithRestrictions = 0x40000000,
	CannotDelete = 0x80000000,
}
