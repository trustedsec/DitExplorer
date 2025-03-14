namespace DitExplorer.Ntds;

public enum ADSecurityPrivilegeFlags
{
	Security = 0x8,
	TakeOwnership = 9,
	Restore = 0x12,
	Debug = 0x14,
	EnableDelegation = 0x1B
}
