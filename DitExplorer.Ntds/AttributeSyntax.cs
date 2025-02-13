using DitExplorer.EseInterop;
using Microsoft.Isam.Esent.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.Ntds;

/// <summary>
/// Represents an attribute syntax.
/// </summary>
/// <remarks>
/// The syntax of an attribute determines how its value is encoded and interpreted.
/// <para>
/// For more information, see <see href="https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-adts/7cda533e-d7a4-4aec-a517-91d02ff4a1aa">the documentation</see>.
/// </para>
/// </remarks>
public class AttributeSyntax : IAttributeSyntax
{
	/// <summary>
	/// Function that retrieves the raw value from a cursor.
	/// </summary>
	internal readonly Func<JetCursor, JET_COLUMNID, int, object?> retrieveFunc;
	/// <summary>
	/// Function that converts the raw value to the presentable value.
	/// </summary>
	/// <remarks>
	/// If no function is present, this means the raw value doesn't require conversion and is returned as-is.
	/// </remarks>
	internal readonly Func<object, object>? decodeFunc;
	/// <summary>
	/// Function to convert from 
	/// </summary>
	private readonly Action<JetCursor, JET_COLUMNID, int, object?> setFunc;

	internal AttributeSyntax(
		Type attributeType,
		string ldapName,
		string syntaxId,
		int prefixEncodedId,
		int omSyntax,
		string? omObjectClass,
		Func<JetCursor, JET_COLUMNID, int, object?> retrieveFunc,
		Func<object, object>? decodeFunc,
		Action<JetCursor, JET_COLUMNID, int, object?> setFunc
		)
	{
		this.AttributeType = attributeType;
		LdapName = ldapName;
		SyntaxId = syntaxId;
		PrefixEncodedId = prefixEncodedId;
		OmSyntax = omSyntax;
		OmObjectClass = omObjectClass;
		this.retrieveFunc = retrieveFunc;
		this.decodeFunc = decodeFunc;
		this.setFunc = setFunc;
	}

	/// <summary>
	/// Gets a value indicating whether the implementation can retrieve a value with this syntax.
	/// </summary>
	public bool CanRetrieveValue => (this.retrieveFunc != null);

	/// <summary>
	/// Gets the type of value held by this attribute.
	/// </summary>
	public Type AttributeType { get; }
	/// <summary>
	/// Gets encoded ID of the syntax.
	/// </summary>
	public int RawId { get; }
	/// <summary>
	/// Gets the LDAP name of the syntax
	/// </summary>
	public string LdapName { get; }
	/// <summary>
	/// Gets the syntax ID.
	/// </summary>
	public string SyntaxId { get; }
	/// <summary>
	/// Gets the prefix-encoded form of the syntax ID.
	/// </summary>
	public int PrefixEncodedId { get; }

	/// <summary>
	/// Gets the omSyntax value.
	/// </summary>
	public int OmSyntax { get; }
	/// <summary>
	/// Gets the omObjectClass, if any.
	/// </summary>
	public string? OmObjectClass { get; }

	internal void SetRawValueOn(JetCursor cursor, JET_COLUMNID colid, object? value)
	{
		if (value is null)
		{
			// TODO: Check if the schema should allow this
			cursor.SetNull(colid);
		}
		else
		{

		}
	}
}
