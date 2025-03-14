using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.Ntds;

public class DirectoryOid : IEquatable<DirectoryOid>, IComparable<DirectoryOid>, IComparable, IFormattable
{
	private readonly int _encodedOid;
	private readonly NtdsDirectory _dir;

	internal DirectoryOid(int encodedOid, NtdsDirectory dir, NtdsSchemaObject? obj = null)
	{
		this._encodedOid = encodedOid;
		this._dir = dir;

		if (obj != null)
		{
			this._attemptedLookup = true;
			this._obj = obj;
		}
	}
	internal DirectoryOid(string text)
	{
		this._text = text;
		this._attemptedLookup = true;
	}


	private string? _text;
	public string OidText => (this._text ??= this.CreateOidText());
	private string? CreateOidText()
	{
		return this._dir.DecodePrefixedOid(this._encodedOid);
	}

	private bool _attemptedLookup;
	private NtdsSchemaObject? _obj;
	public NtdsSchemaObject? Object => (this._attemptedLookup ? this._obj : this.LookupObject());
	private NtdsSchemaObject? LookupObject()
	{
		NtdsSchemaObject? schemaObj = this._dir.TryGetAttributeById(this._encodedOid);
		schemaObj ??= this._dir.TryGetClassByGovernsId(this._encodedOid);
		this._obj = schemaObj;
		this._attemptedLookup = true;
		return this._obj;
	}

	public bool Equals(DirectoryOid? other)
	{
		return (other != null) && (
			(other._encodedOid == this._encodedOid)
			|| (other.OidText.Equals(this.OidText, StringComparison.OrdinalIgnoreCase))
			);
	}
	/// <inheritdoc/>
	public sealed override bool Equals(object? obj)
		=> (obj is DirectoryOid other) && this.Equals(other);
	/// <inheritdoc/>
	public override int GetHashCode()
		=> this.OidText.GetHashCode();
	/// <inheritdoc/>
	public override string ToString()
		=> (this.Object?.LdapDisplayName ?? this.OidText);

	public string ToString(string? format, IFormatProvider? formatProvider)
	{
		if (string.IsNullOrEmpty(format) || format == "L")
			return this.ToString();
		else if (format == "D")
			return this.Object?.DistinguishedName ?? this.OidText;
		else if (format == "O")
			return this.OidText;
		else if (format == "N")
			return this._encodedOid.ToString();
		else if (format == "X")
			return this._encodedOid.ToString("X");
		else
			throw new FormatException();
	}

	public int CompareTo(DirectoryOid? other)
	{
		if (other == null)
			return 1;
		else
			return this.ToString().CompareTo(other.ToString());
	}

	public int CompareTo(object? obj)
		=> (obj is DirectoryOid oid) ? this.CompareTo(oid) : 0;
}
