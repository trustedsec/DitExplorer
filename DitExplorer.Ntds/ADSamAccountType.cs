using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.Ntds;
public enum ADSamAccountType
{
	Domain = 0x0,
	Group = 0x10000000,
	NonSecurityGroup = 0x10000001,
	Alias = 0x20000000,
	NonSecurityAlias = 0x20000001,
	User = 0x30000000,
	NormalUser = 0x30000000,
	Machine = 0x30000001,
	Trust = 0x30000002,
	AppBasicGroup = 0x40000000,
	AppQueryGroup = 0x40000001,
}
