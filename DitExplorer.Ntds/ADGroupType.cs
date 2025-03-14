using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.Ntds;

[Flags]
public enum ADGroupType : uint
{
	None = 0,

	BuiltinLocalGroup = 1,
	AccountGroup = 2,
	ResourceGroup = 4,
	UniversalGroup = 8,
	AppBasicGroup = 0x10,
	QueryGroup = 0x20,
	SecurityEnabled = 0x80000000
}
