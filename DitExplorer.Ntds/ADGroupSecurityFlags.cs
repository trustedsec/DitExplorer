using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.Ntds;

[Flags]
public enum ADGroupSecurityFlags : uint
{
	None = 0,

	Owner = 8,
	UseForDenyOnly = 0x10,
}

[Flags]
public enum ADUserAccountControlFlags : uint
{
	None = 0,
	AccountDisabled=0x02,
	HomedirRequired=8,
	LockedOut=0x10,
	PasswordNotRequired=0x20,
	CantChangePassword=0x40,
	CleartextPasswordAllowed=0x80,
	NormalAccount=0x200,
	InterdomainTrustAccount=0x800,
	Workstation=0x1000,
	Server=0x2000,
	DontExpirePassword=0x1_0000,
	SmartCardRequired=0x4_0000,
	TrustedForDelegation=0x8_0000,
	NotDelegated=0x10_0000,
	UseDesKeyOnly=0x20_0000,
	DontRequirePreauth=0x40_0000,
	PasswordExpired=0x80_0000,
	TrustedToAuthenticateForDelegation=0x100_0000,
	NoAuthDataRequired=0x200_0000,
	PartialSecretsACcount=0x400_0000,
}