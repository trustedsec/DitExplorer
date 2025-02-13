using Microsoft.Isam.Esent.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

/// <summary>
/// Represents an attribute in the directory schema.
/// </summary>
public class NtdsAttributeSchema : NtdsSchemaObject, IAttributeSchema
{
	internal NtdsAttributeSchema(NtdsDirectory dir, int dnt, int parentDnt) : base(dir, dnt, parentDnt)
	{
	}

	/// <summary>
	/// Gets the encoded attribute ID.
	/// </summary>
	/// <remarks>
	/// For more information on how attributes are encoded, see <see href="https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-drsr/6f53317f-2263-48ee-86c1-4580bf97232c">the documentation</see>.
	/// </remarks>
	public int AttributeIdRaw { get; internal set; }

	private string? _rdnToken;
	/// <summary>
	/// Gets the name of this token when it appears in a DN or RDN.
	/// </summary>
	public string RdnToken => (this._rdnToken ??= this.LdapDisplayName?.ToUpper());

	/// <summary>
	/// Gets the name of the column for this attribute in NTDS.dit
	/// </summary>
	public string? ColumnName { get; internal set; }
	/// <summary>
	/// Gets the ID of the column for this attribute in NTDS.dit
	/// </summary>
	internal JET_COLUMNID ColumnId { get; set; }
	/// <summary>
	/// Gets a value indicating whether the column exists in the database.
	/// </summary>
	public bool ExistsInDatabase => (this.ColumnId != JET_COLUMNID.Nil);

	/// <summary>
	/// Gets the prefix-encoded attribute syntax ID.
	/// </summary>
	public int AttributeSyntaxIdPrefixEncoded { get; internal set; }
	/// <summary>
	/// Gets the OM syntax.
	/// </summary>
	public int OmSyntax { get; internal set; }

	/// <summary>
	/// Gets the syntax of the attribute.
	/// </summary>
	/// <remarks>
	/// The syntax of the attribute determines how the value is encoded.
	/// </remarks>
	public AttributeSyntax Syntax { get; internal set; }
	/// <inheritdoc/>
	IAttributeSyntax IAttributeSchema.Syntax => this.Syntax;

	/// <summary>
	/// Gets a value indicating whether the attribute only holds a single value.
	/// </summary>
	public bool IsSingleValued { get; internal set; }
	/// <summary>
	/// Gets the link ID used for this attribute.
	/// </summary>
	public int LinkId { get; internal set; }
	/// <summary>
	/// Gets a value indicating whether this attribute holds links to other objects.
	/// </summary>
	public bool IsLink => this.LinkId != 0;
	/// <summary>
	/// Gets the search flags of this attribute.
	/// </summary>
	public ADSearchFlags SearchFlags { get; internal set; }
	/// <summary>
	/// Gets a value indicating whether this attribute participates in Ambiguous Name Resolution.
	/// </summary>
	public bool UsedForAnr => (0 != (this.SearchFlags & ADSearchFlags.Anr));
}
