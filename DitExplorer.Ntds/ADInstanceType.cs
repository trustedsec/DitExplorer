using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.Ntds;

/// <summary>
/// Specifies instance type flags.
/// </summary>
/// <remarks>
/// These are documented in <see href="https://learn.microsoft.com/en-us/windows/win32/adschema/a-instancetype">Active Directory schema</see>.
/// </remarks>
[Flags]
public enum ADInstanceType
{
	None = 0,

	/// <summary>
	/// Head of naming context
	/// </summary>
	HeadOfNamingContext = 1,
	/// <summary>
	/// Replica is not instantiated
	/// </summary>
	ReplicaNotInstantiated = 2,
	/// <summary>
	/// Object is writable on this directory
	/// </summary>
	Writable = 4,
	/// <summary>
	/// Naming context above this one on this directory is held
	/// </summary>
	ParentNCHeld = 8,
	/// <summary>
	/// Naming context is in the process of being constructed for the first time by using replication
	/// </summary>
	NCUnderConstruction = 0x10,
	/// <summary>
	/// Naming context is in the process of being removed from the local DSA
	/// </summary>
	NCDeleting = 0x20,
};
