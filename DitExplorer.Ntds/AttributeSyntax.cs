using DitExplorer.EseInterop;
using Microsoft.Isam.Esent.Interop;
using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.Ntds;

struct AttrSyntaxInfo
{
	internal string ldapName;
	internal string syntaxId;
	internal int prefixEncodedId;
	internal int omSyntax;
	internal string? omObjectClass;

	public AttrSyntaxInfo(string ldapName, string syntaxId, int prefixEncodedId, int omSyntax, string? omObjectClass = null)
	{
		this.ldapName = ldapName;
		this.syntaxId = syntaxId;
		this.prefixEncodedId = prefixEncodedId;
		this.omSyntax = omSyntax;
		this.omObjectClass = omObjectClass;
	}
}

/// <summary>
/// Represents an attribute syntax.
/// </summary>
/// <remarks>
/// The syntax of an attribute determines how its value is encoded and interpreted.
/// <para>
/// For more information, see <see href="https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-adts/7cda533e-d7a4-4aec-a517-91d02ff4a1aa">the documentation</see>.
/// </para>
/// </remarks>
internal abstract class AttributeSyntax : IAttributeSyntax
{
	protected AttributeSyntax(
		in AttrSyntaxInfo info
		)
	{
		this._info = info;
	}

	private AttrSyntaxInfo _info;

	/// <summary>
	/// Retrieves the raw value from a cursor.
	/// </summary>
	internal abstract object? RetrieveValueFrom(JetCursor cursor, JET_COLUMNID colid, int tag);
	/// <summary>
	/// Converts the raw value to the presentable value.
	/// </summary>
	/// <remarks>
	/// If no function is present, this means the raw value doesn't require conversion and is returned as-is.
	/// </remarks>
	internal abstract object? DecodeValue(object? rawValue);

	/// <summary>
	/// Gets a value indicating whether the implementation can retrieve a value with this syntax.
	/// </summary>
	public bool CanRetrieveValue => true;

	/// <summary>
	/// Gets the type of value held by this attribute, or <see cref="null"/> if the value cannot be decoded.
	/// </summary>
	public abstract Type? AttributeType { get; }
	/// <summary>
	/// Gets the LDAP name of the syntax
	/// </summary>
	public string LdapName => this._info.ldapName;
	/// <summary>
	/// Gets the syntax ID.
	/// </summary>
	public string SyntaxId => this._info.syntaxId;
	/// <summary>
	/// Gets the prefix-encoded form of the syntax ID.
	/// </summary>
	public int PrefixEncodedId => this._info.prefixEncodedId;

	/// <summary>
	/// Gets the omSyntax value.
	/// </summary>
	public int OmSyntax => this._info.omSyntax;
	/// <summary>
	/// Gets the omObjectClass, if any.
	/// </summary>
	public string? OmObjectClass => this._info.omObjectClass;

	/// <inheritdoc/>
	public virtual bool SupportsFlags => false;
	/// <inheritdoc/>
	public virtual bool SupportsParse => false;

	/// <inheritdoc/>
	public virtual object? ParseValue(string text)
		=> throw new NotSupportedException();

	internal virtual void SetRawValueOn(JetCursor cursor, JET_COLUMNID colid, object? value)
	{
		if (value is null)
		{
			// TODO: Check if the schema should allow this
			cursor.SetNull(colid);
		}
		else
		{
			throw new NotImplementedException();
		}
	}

	private IEqualityComparer? _equalityComparer;
	public virtual IEqualityComparer EqualityComparer => (this._equalityComparer ??= EqualityComparer<object>.Default);

	/// <inheritdoc/>
	public virtual bool SupportsComparison => false;

	/// <inheritdoc/>
	public virtual int Compare(object? x, object? y)
		=> throw new NotSupportedException();

	/// <inheritdoc/>
	public bool IsFormattable => false;

	/// <inheritdoc/>
	public AttributeFormat[]? GetDisplayFormats()
		=> Array.Empty<AttributeFormat>();
}

internal sealed class BinaryAttributeSyntax : AttributeSyntax
{
	public BinaryAttributeSyntax(in AttrSyntaxInfo info) : base(info)
	{
	}

	/// <inheritdoc/>
	public sealed override Type? AttributeType => typeof(BinaryData);

	internal sealed override object? RetrieveValueFrom(JetCursor cursor, JET_COLUMNID colid, int tag)
		=> cursor.ReadBytes(colid, tag);
	internal sealed override object? DecodeValue(object? rawValue)
		=> new BinaryData((byte[])rawValue);
	/// <inheritdoc/>
	public sealed override bool SupportsComparison => true;
	/// <inheritdoc/>
	public sealed override int Compare(object? x, object? y)
		=> throw new NotImplementedException();// ((BinaryData)x).CompareTo(y);

	public override bool SupportsParse => true;
	public override object? ParseValue(string text)
		=> BinaryData.Parse(text);
}

internal class AsciiStringAttributeSyntax : AttributeSyntax
{
	public AsciiStringAttributeSyntax(in AttrSyntaxInfo info) : base(info)
	{
	}

	/// <inheritdoc/>
	public sealed override Type? AttributeType => typeof(string);
	/// <inheritdoc/>
	public sealed override IEqualityComparer EqualityComparer => StringComparer.OrdinalIgnoreCase;

	internal override object? RetrieveValueFrom(JetCursor cursor, JET_COLUMNID colid, int tag)
		=> cursor.ReadBytes(colid, tag);
	internal override object? DecodeValue(object? rawValue)
		=> Encoding.ASCII.GetString((byte[])rawValue);
	/// <inheritdoc/>
	public override bool SupportsComparison => true;
	/// <inheritdoc/>
	public override int Compare(object? x, object? y)
		=> ((string)x).CompareTo(y);

	public override bool SupportsParse => true;
	public override object? ParseValue(string text)
		=> text;
}

internal class Utf16StringAttributeSyntax : AttributeSyntax
{
	public Utf16StringAttributeSyntax(in AttrSyntaxInfo info) : base(info)
	{
	}

	/// <inheritdoc/>
	public override Type? AttributeType => typeof(string);
	/// <inheritdoc/>
	public sealed override IEqualityComparer EqualityComparer => StringComparer.OrdinalIgnoreCase;

	internal override object? RetrieveValueFrom(JetCursor cursor, JET_COLUMNID colid, int tag)
		=> cursor.ReadUtf16String(colid, tag);
	internal override object? DecodeValue(object? rawValue)
		=> (string)rawValue;
	/// <inheritdoc/>
	public override bool SupportsComparison => true;
	/// <inheritdoc/>
	public override int Compare(object? x, object? y)
		=> ((string)x).CompareTo(y);

	public override bool SupportsParse => true;
	public override object? ParseValue(string text)
		=> text;
}

internal class Int32AttributeSyntax : AttributeSyntax
{
	public Int32AttributeSyntax(in AttrSyntaxInfo info) : base(info)
	{
	}

	/// <inheritdoc/>
	public override Type? AttributeType => typeof(int);
	private static IEqualityComparer? _comparer;
	/// <inheritdoc/>
	public sealed override IEqualityComparer EqualityComparer => (_comparer ??= EqualityComparer<int>.Default);

	internal override object? RetrieveValueFrom(JetCursor cursor, JET_COLUMNID colid, int tag)
		=> cursor.ReadInt32(colid, tag);
	internal override object? DecodeValue(object? rawValue)
		=> (int)rawValue;
	/// <inheritdoc/>
	public override bool SupportsComparison => true;
	/// <inheritdoc/>
	public override int Compare(object? x, object? y)
		=> ((int)x).CompareTo(y);

	public override bool SupportsParse => true;
	public override object? ParseValue(string text)
		=> int.Parse(text);
}

internal class Int64AttributeSyntax : AttributeSyntax
{
	public Int64AttributeSyntax(in AttrSyntaxInfo info) : base(info)
	{
	}

	/// <inheritdoc/>
	public override Type? AttributeType => typeof(long);
	private static IEqualityComparer? _comparer;
	/// <inheritdoc/>
	public override IEqualityComparer EqualityComparer => (_comparer ??= EqualityComparer<long>.Default);

	internal override object? RetrieveValueFrom(JetCursor cursor, JET_COLUMNID colid, int tag)
		=> cursor.ReadInt64(colid, tag);
	internal override object? DecodeValue(object? rawValue)
		=> (long)rawValue;

	/// <inheritdoc/>
	public override bool SupportsComparison => true;
	/// <inheritdoc/>
	public override int Compare(object? x, object? y)
		=> ((long)x).CompareTo(y);

	public override bool SupportsParse => true;
	public override object? ParseValue(string text)
		=> long.Parse(text);
}

internal sealed class BoolAttributeSyntax : AttributeSyntax
{
	public BoolAttributeSyntax(in AttrSyntaxInfo info) : base(info)
	{
	}

	public sealed override Type? AttributeType => typeof(bool);
	private static IEqualityComparer? _comparer;
	/// <inheritdoc/>
	public sealed override IEqualityComparer EqualityComparer => (_comparer ??= EqualityComparer<bool>.Default);

	internal override object? RetrieveValueFrom(JetCursor cursor, JET_COLUMNID colid, int tag)
		=> cursor.ReadInt32(colid, tag);
	internal override object? DecodeValue(object? rawValue)
		=> ((int)rawValue) != 0;

	internal override void SetRawValueOn(JetCursor cursor, JET_COLUMNID colid, object? value)
	{
		if (value is null)
			cursor.SetNull(colid);
		else
			cursor.SetInt32(colid, 1, (int)value);
	}

	public override object? ParseValue(string text)
		=> bool.Parse(text);
}

internal sealed class OidAttributeSyntax : AttributeSyntax
{
	private readonly NtdsDirectory dir;

	public OidAttributeSyntax(in AttrSyntaxInfo info, NtdsDirectory dir) : base(info)
	{
		this.dir = dir;
	}

	public sealed override Type? AttributeType => typeof(DirectoryOid);
	/// <inheritdoc/>
	public sealed override IEqualityComparer EqualityComparer => StringComparer.Ordinal;

	internal override object? RetrieveValueFrom(JetCursor cursor, JET_COLUMNID colid, int tag)
		=> cursor.ReadInt32(colid, tag);
	internal override object? DecodeValue(object? rawValue)
	{
		int id = (int)rawValue;
		return new DirectoryOid(id, this.dir);
	}

	internal override void SetRawValueOn(JetCursor cursor, JET_COLUMNID colid, object? value)
	{
		if (value is null)
			cursor.SetNull(colid);
		else
			cursor.SetInt32(colid, 1, (int)value);
	}

	public override bool SupportsParse => true;
	public override object? ParseValue(string text)
	{
		{
			var cls = this.dir.TryGetClassByLdapName(text);
			if (cls != null)
				return new DirectoryOid(cls.GovernsIdRaw, this.dir, cls);
		}
		{
			var attr = this.dir.TryGetAttributeByLdapName(text);
			if (attr != null)
				return new DirectoryOid(attr.AttributeIdRaw, this.dir, attr);
		}

		// TODO: Parse
		return new DirectoryOid(text);
	}
}

internal class Int32EnumAttributeSyntax<TEnum> : AttributeSyntax
{
	public Int32EnumAttributeSyntax(in AttrSyntaxInfo info) : base(info)
	{
	}

	/// <inheritdoc/>
	public override Type? AttributeType => typeof(TEnum);
	private static IEqualityComparer? _comparer;
	/// <inheritdoc/>
	public sealed override IEqualityComparer EqualityComparer => (_comparer ??= EqualityComparer<int>.Default);

	internal override object? RetrieveValueFrom(JetCursor cursor, JET_COLUMNID colid, int tag)
		=> cursor.ReadInt32(colid, tag);
	internal override object? DecodeValue(object? rawValue)
		=> Enum.ToObject(typeof(TEnum), (int)rawValue);
	/// <inheritdoc/>
	public override bool SupportsComparison => true;
	/// <inheritdoc/>
	public override int Compare(object? x, object? y)
		=> ((int)x).CompareTo(y);

	/// <inheritdoc/>
	public override bool SupportsFlags => true;
	/// <inheritdoc/>
	public override bool SupportsParse => true;
	public override object? ParseValue(string text)
		=> Enum.Parse(typeof(TEnum), text);
}

internal class DateTimeAttributeSyntax : Int64AttributeSyntax
{
	private readonly bool local;

	public DateTimeAttributeSyntax(in AttrSyntaxInfo info, bool local) : base(info)
	{
		this.local = local;
	}

	private static IEqualityComparer? _comparer;
	/// <inheritdoc/>
	public sealed override IEqualityComparer EqualityComparer => (_comparer ??= EqualityComparer<DateTime>.Default);

	internal override object? DecodeValue(object? rawValue)
	{
		long value = (long)rawValue;
		if (value == DirectoryTime.NeverTicks)
			return DirectoryTime.Never;
		else if (value == 0)
			return DirectoryTime.Empty;
		else
			return new DirectoryTime(this.local ? DateTime.FromFileTime(value * 10_000_000)
				: DateTime.FromFileTimeUtc(value * 10_000_000), value);
	}

	public override int Compare(object? x, object? y)
		=> ((DirectoryTime)x).CompareTo(y);

	/// <inheritdoc/>
	public override Type? AttributeType => typeof(DirectoryTime);
	public override bool SupportsParse => true;
	public override object? ParseValue(string text)
		=> DirectoryTime.Parse(text, this.local ? DateTimeKind.Local : DateTimeKind.Utc, 10_000_000);
}

internal class FileTimeAttributeSyntax : Int64AttributeSyntax
{
	private readonly bool local;

	public FileTimeAttributeSyntax(in AttrSyntaxInfo info, bool local) : base(info)
	{
		this.local = local;
	}

	private static IEqualityComparer? _comparer;
	/// <inheritdoc/>
	public sealed override IEqualityComparer EqualityComparer => (_comparer ??= EqualityComparer<DateTime>.Default);

	internal override object? DecodeValue(object? rawValue)
	{
		long value = (long)rawValue;
		if (value == 0x7FFFFFFF_FFFFFFFF)
			return DirectoryTime.Never;
		else if (value == 0)
			return DirectoryTime.Empty;
		else
			return new DirectoryTime(this.local ? DateTime.FromFileTime(value)
				: DateTime.FromFileTimeUtc(value), value);
	}

	public override int Compare(object? x, object? y)
		=> ((DirectoryTime)x).CompareTo(y);

	/// <inheritdoc/>
	public override Type? AttributeType => typeof(DirectoryTime);
	public override bool SupportsParse => true;
	public override object? ParseValue(string text)
		=> DirectoryTime.Parse(text, this.local ? DateTimeKind.Local : DateTimeKind.Utc, 1);
}

internal class SidAttributeSyntax : AttributeSyntax
{
	public SidAttributeSyntax(in AttrSyntaxInfo info) : base(info)
	{
	}

	/// <inheritdoc/>
	public override Type? AttributeType => typeof(SecurityIdentifier);
	private static IEqualityComparer? _comparer;
	/// <inheritdoc/>
	public sealed override IEqualityComparer EqualityComparer => (_comparer ??= EqualityComparer<SecurityIdentifier>.Default);

	internal override object? RetrieveValueFrom(JetCursor cursor, JET_COLUMNID colid, int tag)
		=> cursor.ReadBytes(colid, tag);
	internal override object? DecodeValue(object? rawValue)
		=> NtdsDirectory.DecodeDirectorySid((byte[])rawValue);

	/// <inheritdoc/>
	public override bool SupportsComparison => false;

	public override bool SupportsParse => true;
	public override object? ParseValue(string text)
	{
		var sid = new SecurityIdentifier(text);
		return sid;
	}
}

internal class SecDescAttributeSyntax : AttributeSyntax
{
	private readonly NtdsDirectory dir;

	public SecDescAttributeSyntax(in AttrSyntaxInfo info, NtdsDirectory dir) : base(info)
	{
		this.dir = dir;
	}

	/// <inheritdoc/>
	public override Type? AttributeType => typeof(RawSecurityDescriptor);
	private static IEqualityComparer? _comparer;
	/// <inheritdoc/>
	public sealed override IEqualityComparer EqualityComparer => (_comparer ??= EqualityComparer<RawSecurityDescriptor>.Default);

	internal override object? RetrieveValueFrom(JetCursor cursor, JET_COLUMNID colid, int tag)
		=> cursor.ReadBytes(colid, tag);
	internal override object? DecodeValue(object? rawValue)
		=> this.dir.TryGetSecurityDescriptor((byte[])rawValue);

	/// <inheritdoc/>
	public override bool SupportsComparison => false;

	public override bool SupportsParse => true;
	public override object? ParseValue(string text)
	{
		var sd = new RawSecurityDescriptor(text);
		return sd;
	}
}

internal sealed class GuidAttributeSyntax : AttributeSyntax
{
	public GuidAttributeSyntax(in AttrSyntaxInfo info) : base(info)
	{
	}

	/// <inheritdoc/>
	public sealed override Type? AttributeType => typeof(Guid);
	private static IEqualityComparer? _comparer;
	/// <inheritdoc/>
	public sealed override IEqualityComparer EqualityComparer => (_comparer ??= EqualityComparer<Guid>.Default);

	internal sealed override object? RetrieveValueFrom(JetCursor cursor, JET_COLUMNID colid, int tag)
		=> cursor.ReadBytes(colid, tag);
	internal sealed override object? DecodeValue(object? rawValue)
		=> new Guid((byte[])rawValue);
	/// <inheritdoc/>
	public sealed override bool SupportsComparison => true;
	/// <inheritdoc/>
	public sealed override int Compare(object? x, object? y)
		=> ((Guid)x).CompareTo(y);

	public override bool SupportsParse => true;
	public override object? ParseValue(string text)
		=> Guid.Parse(text);
}

internal class DntAttributeSyntax : AttributeSyntax
{
	private readonly NtdsDirectory dir;

	public DntAttributeSyntax(
		in AttrSyntaxInfo info,
		NtdsDirectory dir
		) : base(info)
	{
		this.dir = dir;
	}

	/// <inheritdoc/>
	public sealed override Type? AttributeType => typeof(NtdsDirectoryObjectReference);
	private static IEqualityComparer? _comparer;
	/// <inheritdoc/>
	public sealed override IEqualityComparer EqualityComparer => (_comparer ??= EqualityComparer<NtdsDirectoryObjectReference>.Default);

	internal sealed override object? RetrieveValueFrom(JetCursor cursor, JET_COLUMNID colid, int tag)
		=> cursor.ReadInt32(colid, tag);
	internal sealed override object? DecodeValue(object? rawValue)
		=> new NtdsDirectoryObjectReference(this.dir, (int)rawValue);
	/// <inheritdoc/>
	public sealed override bool SupportsComparison => false;
	public override bool SupportsParse => true;
	public override object? ParseValue(string text)
	{
		NtdsDirectoryObject obj = this.dir.TryGetClassByLdapName(text);
		if (obj == null)
			obj = this.dir.TryGetAttributeByLdapName(text);

		// TODO: Parse DN as well
		if (obj != null)
			return obj;
		else if (text.Contains('='))
		{
			obj = this.dir.GetObjectByDn(text);
			return new NtdsDirectoryObjectReference(obj);
		}
		else
			throw new FormatException("The text does not identify a recognized object.");
	}
}

internal class ObjRefAttributeSyntax : AttributeSyntax
{
	private readonly NtdsDirectory dir;

	public ObjRefAttributeSyntax(
		in AttrSyntaxInfo info,
		NtdsDirectory dir
		) : base(info)
	{
		this.dir = dir;
	}

	/// <inheritdoc/>
	public sealed override Type? AttributeType => typeof(NtdsDirectoryObjectReference);
	private static IEqualityComparer? _comparer;
	/// <inheritdoc/>
	public sealed override IEqualityComparer EqualityComparer => (_comparer ??= EqualityComparer<NtdsDirectoryObjectReference>.Default);

	internal sealed override object? RetrieveValueFrom(JetCursor cursor, JET_COLUMNID colid, int tag)
		=> cursor.ReadBytes(colid, tag);
	internal sealed override object? DecodeValue(object? rawValue)
	{
		var bytes = (byte[])rawValue;
		int dnt = BinaryPrimitives.ReadInt32LittleEndian(bytes);
		if (bytes.Length == 8 + 16)
		{
			// Treat it like a GUID
			Guid guid = new Guid(bytes.AsSpan().Slice(8));
			return new NtdsDirectoryObjectReference<Guid>(this.dir, dnt, guid);
		}
		else
		{
			return new NtdsDirectoryObjectReference<BinaryData>(this.dir, dnt, new BinaryData(bytes.AsSpan().Slice(8).ToArray()));
		}
	}
	/// <inheritdoc/>
	public sealed override bool SupportsComparison => false;
	public override bool SupportsParse => false;
	public override object? ParseValue(string text)
	{
		throw new NotImplementedException();
	}
}
