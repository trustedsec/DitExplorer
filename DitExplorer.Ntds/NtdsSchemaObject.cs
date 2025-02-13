
namespace DitExplorer.Ntds;

/// <summary>
/// Represents a schema object.
/// </summary>
public abstract class NtdsSchemaObject : NtdsDirectoryObject, ISchemaObject
{
	internal NtdsSchemaObject(NtdsDirectory dir, int dnt, int parentDnt) : base(dir, dnt, parentDnt)
	{
	}

	/// <inheritdoc/>
	public sealed override string ToString() => this.LdapDisplayName;

	/// <summary>
	/// Gets the LDAP display name of the object.
	/// </summary>
	public string? LdapDisplayName { get; internal set; }

	/// <summary>
	/// Gets the admin description.
	/// </summary>
	public string? AdminDescription { get; internal set; }
}
