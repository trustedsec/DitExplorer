using DitExplorer.EseInterop;
using DitExplorer.Ntds;
using Microsoft.Isam.Esent;
using Microsoft.Isam.Esent.Interop;
using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.DataContracts;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;

namespace DitExplorer.Ntds;

public enum DirectoryOpenOptions
{
	None = 0,

	ReadOnly = 1,
}

/// <summary>
/// Represents a directory.
/// </summary>
/// <remarks>
/// Use <see cref="Open(string, string?, DirectoryOpenOptions)"/> to open a DIT file.
/// </remarks>
public partial class NtdsDirectory : IDirectory
{
	/// <summary>
	/// Size of pages in NTDS.dit
	/// </summary>
	private const int NtdsPageSize = 8192;
	private const string DataTableName = "datatable";
	private const string NameIndex = "INDEX_00090001";
	private const string DntIndexName = "DNT_index";
	private const string ParentDntNameIndex = "PDNT_index";
	private const string AncestorsIndex = "Ancestors_index";
	private const string ObjectSidIndex = "INDEX_00090092";
	private const string PrimaryGroupIdIndex = "INDEX_00090062";

	private const string SdTableName = "sd_table";
	private const string SdValueColName = "sd_value";
	private const string SdIdIndexName = "sd_id_index";
	private const string SdHashIndexName = "sd_hash_index";

	private const string LinkTableName = "link_table";
	private const string LinkIndexName = "link_index";
	private const string BacklinkIndexName = "backlink_index";
	private static readonly string[] LinkTableColumnNames = new string[]
	{
		"backlink_DNT",
		"link_base",
		"link_DNT",
	};

	private readonly JetInstance instance;
	private JetSession session;
	private JetCursor _dntCursor;
	private JetCursor _sdCursor;
	private JET_COLUMNID[] linkTableColIds;
	private JET_COLUMNID _sdValueColid;

	/// <summary>
	/// Gets the database underlying the directory.
	/// </summary>
	/// <remarks>
	/// Be careful what you do with database, as you may interfere
	/// with the operation of the directory and cause it to behave
	/// in unexpected ways.
	/// </remarks>
	public JetDatabase Database { get; }

	enum SpecialColumn
	{
		// Hierarchy and structure
		Dnt,
		Pdnt,
		Ancestors,
		RdnType,
		Ncdnt,
		Name,
		cn,
		isDeleted,
		instanceType,
		objectClass,

		adminDescription,
		// Common schema
		ldapDisplayName,
		// Attribute schema
		attributeSyntax,
		omSyntax,
		attributeId,
		isSingleValued,
		linkId,
		searchFlags,
		// Class schema
		governsId,
		subClassOf,
		auxiliaryClass,
		systemAuxiliaryClass,
		mustContain,
		systemMustContain,
		mayContain,
		systemMayContain,
		// Security
		objectSid,
		primaryGroupId,
	}

	private static readonly string[] specialColumnNames = new string[]
	{
		"DNT_col",
		"PDNT_col",
		"Ancestors_col",
		"RDNtyp_col",
		"NCDNT_col",
		"ATTm589825",
		"ATTm3",
		"ATTi131120",
		"ATTj131073",
		"ATTc0",

		"ATTm131298",
		// Common schema
		"ATTm131532",
		// Attribute schema
		"ATTc131104",
		"ATTj131303",
		"ATTc131102",
		"ATTi131105",
		"ATTj131122",
		"ATTj131406",
		// Class schema
		"ATTc131094",
		"ATTc131093",
		"ATTc131423",
		"ATTc590022",
		"ATTc131096",
		"ATTc590021",
		"ATTc131097",
		"ATTc590020",
		// Security
		"ATTj589922",
		"ATTr589970",
	};

	private const string ConfigurationName = "Configuration";
	private const string SchemaName = "Schema";
	/// <summary>
	/// governsID of the objectClass class
	/// </summary>
	private const int GovernsIdOfObjClass = 196621;
	private const int AttrIdId = 196622;
	private const int ObjectSidAttrId = 589970;
	private const int PrimaryGroupIdAttrId = 589922;
	private const int MemberOfAttrId = 131174;
	private const int MemberAttrId = 31;

	/// <summary>
	/// Opens an offline NTDS.dit database.
	/// </summary>
	/// <param name="fileName">Name of NTDS.dit file</param>
	/// <param name="rootDomain">DN of root domain</param>
	/// <param name="options"></param>
	/// <returns></returns>
	/// <exception cref="ArgumentException"><paramref name="fileName"/> is <see langword="null"/></exception>
	/// <exception cref="Exception">An error occurred while opening the database</exception>
	public static NtdsDirectory Open(string fileName, string? rootDomain, DirectoryOpenOptions options = DirectoryOpenOptions.None)
	{
		if (string.IsNullOrEmpty(fileName)) throw new ArgumentException($"'{nameof(fileName)}' cannot be null or empty.", nameof(fileName));

		JetSession? session;
		JetInstance? instance;
		try
		{
			// Initialize ESE
			(instance, session) = CreateEseSession();
		}
		catch (Exception ex)
		{
			throw new Exception("An error occurred while initializing ESE: " + ex.Message, ex);
		}

		JetDatabase? db = null;
		try
		{
			// Open the database
			OpenDatabaseOptions dbOptions =
				(0 != (options & DirectoryOpenOptions.ReadOnly)) ? OpenDatabaseOptions.ReadOnly
				: OpenDatabaseOptions.None;
			db = session.AttachAndOpen(fileName, dbOptions);

			// Instantiate the directory
			NtdsDirectory? dir = new NtdsDirectory(instance, session, db, null);
			instance = null;
			session = null;
			db = null;
			return dir;
		}
		finally
		{
			db?.Dispose();
			session?.Dispose();
			instance?.Dispose();
		}
	}

	static NtdsDirectory()
	{
		InitEse();
	}


	private static Exception? _initException;
	private static void InitEse()
	{
		try
		{
			var nilInstance = JET_INSTANCE.Nil;
			Api.JetSetSystemParameter(nilInstance, JET_SESID.Nil, JET_param.DatabasePageSize, NtdsPageSize, null);
			Api.JetSetSystemParameter(nilInstance, JET_SESID.Nil, JET_param.RuntimeCallback, new JET_CALLBACK(JetCallback), null);
		}
		catch (Exception ex)
		{
			_initException = ex;
		}
	}

	private static (JetInstance, JetSession) CreateEseSession()
	{
		if (_initException != null)
			throw new Exception("An error occurred while initializing ESE.  You must restart the application.", _initException);

		JetInstance? inst = null;
		JetSession? session = null;
		try
		{
			inst = JetInstance.Initialize();
			session = inst.BeginSession();

			var result = (inst, session);
			inst = null;
			session = null;
			return result;
		}
		finally
		{
			session?.Dispose();
			inst?.Dispose();
		}
	}

	private static JET_err JetCallback(
		JET_SESID sesid,
		JET_DBID dbid,
		JET_TABLEID tableid,
		JET_cbtyp cbtyp,
		object arg1,
		object arg2,
		IntPtr context,
		IntPtr unused)
	{
		return 0;
	}

	/// <summary>
	/// Initializes a new <see cref="NtdsDirectory"/>
	/// </summary>
	/// <exception cref="Exception">A problem was encountered while loading the directory.</exception>
	private NtdsDirectory(
		JetInstance instance,
		JetSession session,
		JetDatabase db,
		X500DistinguishedName? rootDN)
	{
		if (rootDN != null)
			// TODO: Support rootDN parameter
			throw new NotImplementedException();

		this.instance = instance;
		this.session = session;
		this.Database = db;

		// Look up IDs of special columns in datatable
		// TODO: Maybe do a sanity check to see if this really is NTDS.dit
		{
			var table = db.GetTableInfo(DataTableName);
			var colids = new JET_COLUMNID[specialColumnNames.Length];
			for (int i = 0; i < colids.Length; i++)
			{
				var name = specialColumnNames[i];
				colids[i] = table.GetColumnId(name);
			}
			this._specialColumnIds = colids;
		}

		// Look up link_table columns
		{
			var table = db.GetTableInfo(LinkTableName);
			JET_COLUMNID[] linkTableCols = Array.ConvertAll(LinkTableColumnNames, r => table.GetColumnId(r));
			this.linkTableColIds = linkTableCols;
		}

		{
			var dntCursor = db.OpenTable(DataTableName);
			dntCursor.SetIndex(DntIndexName);
			this._dntCursor = dntCursor;
		}

		{
			var sdCursor = db.OpenTable(SdTableName);
			sdCursor.SetIndex(SdIdIndexName);
			this._sdValueColid = db.GetColumnId(SdTableName, SdValueColName);
			this._sdCursor = sdCursor;
		}

		if (rootDN is not null)
		{
			int dnt = 0;
			// TODO: Parse root DN
			var parts = rootDN.Name.Split(',');
			foreach (var part in parts)
			{
			}
			throw new NotImplementedException();
		}
		else
		{
			// No root domain provided, try to detect the root domain
			// Start at the root of the database and return the first NC head
			var rootObj = this.GetChildrenOf(0).FirstOrDefault();
			NtdsDirectoryObject? rootDomainObj = null;
			if (rootObj != null)
			{
				int dnt = rootObj.Dnt;
				do
				{
					bool foundChild = false;
					foreach (var obj in this.GetChildrenOf(dnt))
					{
						// Skip deleted objects
						if (obj.IsDeleted)
							continue;

						foundChild = true;
						if (obj.IsNCHead)
							// NC head, assume it's the root domain
							rootDomainObj = obj;
						else
							// Nope, keep going
							dnt = obj.Dnt;

						break;
					}
					if (!foundChild)
						break;
				} while (rootDomainObj == null);
			}

			if (rootDomainObj == null)
				throw new Exception(Messages.Directory_NoRootFound);

			this.RootDomain = rootDomainObj;
		}

		this.LoadSyntaxes();
		this.LoadSchema();
	}

	#region Schema
	private JET_COLUMNID[] _specialColumnIds;
	private List<NtdsClassSchema> _classes;
	private Dictionary<string, NtdsClassSchema> _classesByLdapName;
	private Dictionary<int, NtdsClassSchema> _classesByGovernsId;
	private List<NtdsAttributeSchema> _attrs;
	private List<NtdsAttributeSchema> _anrAttrs;
	private Dictionary<string, NtdsAttributeSchema> _attrsByLdapName;
	private Dictionary<int, NtdsAttributeSchema> _attrsById;

	private void LoadSchema()
	{
		List<NtdsClassSchema> objClasses = new List<NtdsClassSchema>();
		List<NtdsAttributeSchema> attrSchemas = new List<NtdsAttributeSchema>();
		List<NtdsAttributeSchema> anrAttrs = new List<NtdsAttributeSchema>();
		Dictionary<int, NtdsClassSchema> classesByGovernsId = new Dictionary<int, NtdsClassSchema>();
		Dictionary<string, NtdsClassSchema> classesByLdapDisplayName = new Dictionary<string, NtdsClassSchema>(StringComparer.OrdinalIgnoreCase);
		Dictionary<int, NtdsAttributeSchema> attrsById = new Dictionary<int, NtdsAttributeSchema>();
		Dictionary<string, NtdsAttributeSchema> attrsByLdapDisplayName = new Dictionary<string, NtdsAttributeSchema>(StringComparer.OrdinalIgnoreCase);

		var schemaObj = this.RootDomain.GetChild(ConfigurationName).GetChild(SchemaName);
		foreach (var item in schemaObj.GetChildren())
		{
			if (item is NtdsClassSchema objcls)
			{
				objClasses.Add(objcls);
				classesByLdapDisplayName.Add(objcls.LdapDisplayName, objcls);

				var governsId = objcls.GovernsIdRaw;
				classesByGovernsId.Add(governsId, objcls);
			}
			else if (item is NtdsAttributeSchema attr)
			{
				attrSchemas.Add(attr);
				attrsByLdapDisplayName.Add(attr.LdapDisplayName, attr);

				var attrId = attr.AttributeIdRaw;
				attrsById.Add(attrId, attr);

				if (attr.UsedForAnr)
					anrAttrs.Add(attr);
			}
		}

		this._classes = objClasses;
		this._classesByLdapName = classesByLdapDisplayName;
		this._classesByGovernsId = classesByGovernsId;
		this._attrs = attrSchemas;
		this._anrAttrs = anrAttrs;
		this._attrsByLdapName = attrsByLdapDisplayName;
		this._attrsById = attrsById;

		NtdsClassSchema? TryGetClass(int governsId)
		{
			classesByGovernsId.TryGetValue(governsId, out var baseClass);
			return baseClass;
		}

		// Link up classes
		foreach (var objcls in this._classes)
		{
			U?[] ResolveList<T, U>(Nullable<T>[] values, Func<T, U?> converter)
				where T : struct
			{
				if (values == null)
					return Array.Empty<U>();

				List<U> results = new List<U>(values.Length);
				foreach (var item in values)
				{
					if (item != null)
					{
						var result = converter(item.Value);
						if (result != null)
							results.Add(result);
					}
				}

				return results.ToArray();
			}

			if (objcls.SubclassOfId != objcls.GovernsIdRaw)
				// Don't follow the top -> top cycle
				objcls.SuperClass = TryGetClass(objcls.SubclassOfId);
		}
	}

	/// <summary>
	/// OID prefix table
	/// </summary>
	/// <remarks>
	/// Derived from https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-drsr/6f53317f-2263-48ee-86c1-4580bf97232c
	/// </remarks>
	private static readonly string[] OidPrefixes = new string[]
	{
		// 0
		"2.5.4",
		"2.5.6",
		"1.2.134.72.134.247.20.1.2",
		"1.2.134.72.134.247.20.1.3",
		"2.16.134.72.1.101.2.2.1",
		"2.16.134.72.1.101.2.2.3",
		"2.16.134.72.1.101.2.1.5",
		"2.16.134.72.1.101.2.1.4",
		"2.5.5",
		"1.2.134.72.134.247.20.1.4",
		// 10
		"1.2.134.72.134.247.20.1.5",
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		"0.9.146.38.137.147.242.44.100",
		// 20
		"2.16.134.72.1.134.248.66.3",
		"0.9.146.38.137.147.242.44.100.1",
		"2.16.134.72.1.134.248.66.3.1",
		"1.2.134.72.134.247.20.1.5.182.88",
		"2.5.21",
		"2.5.18",
		"2.5.20",
		"1.3.6.1.4.1.1466.101.119",
		"2.16.840.1.113730.3.2",
		"1.3.6.1.4.1.250.1",
		// 30
		"1.2.840.113549.1.9",
		"0.9.2342.19200300.100.4",
		"1.2.840.113556.1.6.23",
		"1.2.840.113556.1.6.18.1",
		"1.2.840.113556.1.6.18.2",
		"1.2.840.113556.1.6.13.3",
		"1.2.840.113556.1.6.13.4",
		"1.3.6.1.1.1.1",
		"1.3.6.1.1.1.2",
	};
	/// <summary>
	/// Decodes a prefix-encoded OID.
	/// </summary>
	/// <param name="encoded">Prefix-encoded OID</param>
	/// <returns></returns>
	private static string DecodePrefixedOid(int encoded)
	{
		var prefixId = (encoded >> 16);
		string prefix;
		if (prefixId >= OidPrefixes.Length)
		{
#if DEBUG
			// This only exists so I can easily add OIDs while debugging
			prefix = prefixId switch
			{
				_ => null,
			};
#else
			prefix = null;
#endif
		}
		else
		{
			prefix = OidPrefixes[prefixId];
		}

		if (prefix == null)
			prefix = "<unknown prefix>";
		// TODO: Should this throw an error?
		//throw new ArgumentOutOfRangeException(nameof(encoded));

		string oid = prefix + "." + (encoded & 0xFFFF);
		return oid;
	}

	private static string? DecodeBerOid(ReadOnlySpan<byte> ber)
	{
		if (ber.Length == 0)
			return null;

		StringBuilder sb = new StringBuilder(ber.Length * 3);
		sb.Append(ber[0] / 40).Append('.').Append(ber[0] % 40);
		for (int i = 1; i < ber.Length; i++)
		{
			sb.Append('.').Append(ber[i]);
		}

		return sb.ToString();
	}



	private AttributeSyntax[] _syntaxes;

	record struct SyntaxKey(int oid, int omSyntax);
	private Dictionary<SyntaxKey, AttributeSyntax> _syntaxesByKey;

	// Special syntaxes
	private AttributeSyntax _guidSyntax;
	private AttributeSyntax _instanceTypeSyntax;
	private AttributeSyntax _systemFlagsSyntax;
	private AttributeSyntax _searchFlagsSyntax;

	/// <summary>
	/// Creates the attribute syntaxes used by attributes.
	/// </summary>
	/// <remarks>
	/// This is based on the table in <see href="https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-adts/7cda533e-d7a4-4aec-a517-91d02ff4a1aa">[MS-ADTS] § 3.1.1.2.2.2 - LDAP Representations</see>.
	/// </remarks>
	private void LoadSyntaxes()
	{
		Func<JetCursor, JET_COLUMNID, int, object> readBytes = (csr, colid, tag) => csr.ReadBytes(colid, tag);
		Func<JetCursor, JET_COLUMNID, int, object> readGuid = (csr, colid, tag) =>
		{
			var bytes = csr.ReadBytes(colid, tag);
			return (bytes != null) ? new Guid(bytes) : null;
		};
		Func<JetCursor, JET_COLUMNID, int, object> readInt32 = (csr, colid, tag) => csr.ReadInt32(colid, tag);
		Func<JetCursor, JET_COLUMNID, int, object> readInt64 = (csr, colid, tag) => csr.ReadInt64(colid, tag);
		Func<JetCursor, JET_COLUMNID, int, object> readUtf16String = (csr, colid, tag) => csr.ReadUtf16String(colid, tag);

		Action<JetCursor, JET_COLUMNID, int, object> writeBytes = (csr, colid, tag, value) => csr.SetColumnBytes(colid, tag, (byte[])value);
		Action<JetCursor, JET_COLUMNID, int, object> writeInt32 = (csr, colid, tag, value) => csr.SetInt32(colid, tag, (int)value);
		Action<JetCursor, JET_COLUMNID, int, object> writeInt64 = (csr, colid, tag, value) => csr.SetInt64(colid, tag, (long)value);
		Action<JetCursor, JET_COLUMNID, int, object> writeUtf16String = (csr, colid, tag, value) => csr.SetUtf16String(colid, tag, ((string)value) + '\0');

		Func<object, object> decodeBoolean = r => ((int)r) != 0;
		Func<object, object> decodeAnsiString = r => Encoding.UTF8.GetString((byte[])r);
		Func<object, object> decodeUtcTime = r => DateTime.FromFileTimeUtc(((long)r) * 10_000_000);
		Func<object, object> decodeGeneralTime = r => DateTime.FromFileTime(((long)r) * 10_000_000);
		Func<object, object> decodeSid = r => ParseDirectorySid(r);
		Func<object, object> decodeSD = r => DecodeSD((byte[])r);
		Func<object, object> decodeOid = r => DecodePrefixedOid((int)r);
		Func<object, object> resolveDnt = r => this.GetByDnt((int)r);

		// TODO: In general, verify attribute decoding and fill out this table

		var syntaxes = new AttributeSyntax[]
		{
			new AttributeSyntax(typeof(bool), "Boolean", "2.5.5.8", 0x80000 + 8, 1, null, readInt32, decodeBoolean, writeInt32),
			new AttributeSyntax(typeof(int), "Enumeration", "2.5.5.9", 0x80000 + 9, 10, null, readInt32, null, writeInt32),
			new AttributeSyntax(typeof(int), "Integer", "2.5.5.9", 0x80000 + 9, 2, null, readInt32, null, writeInt32),
			new AttributeSyntax(typeof(long), "LargeInteger", "2.5.5.16", 0x80000 + 16, 64, null, readInt64, null, writeInt64),
			// TODO: No examples found, could be UTF-16 string or binary
			//new AttributeSyntax("Object(Access-Point)", "2.5.5.14", 0x80000 + 14, 127, "1.3.12.2.1011.28.0.702", null, null),
			// TODO: No examples found, could be UTF-16 string or binary
			new AttributeSyntax(null, "Object(DN-String)", "2.5.5.14", 0x80000 + 14, 127, "1.2.840.113556.1.1.1.12", null, null, null),
			//new AttributeSyntax("Object(OR-Name)", "2.5.5.7", 0x80000 + 7, 127, "2.6.6.1.2.5.11.29", readBytes, null),
			// TODO: Decoder
			new AttributeSyntax(typeof(byte[]), "Object(DN-Binary)", "2.5.5.7", 0x80000 + 7, 127, "1.2.840.113556.1.1.1.11", readBytes, null, writeBytes),
			new AttributeSyntax(typeof(NtdsDirectoryObject), "Object(DS-DN)", "2.5.5.1", 0x80000 + 1, 127, "1.3.12.2.1011.28.0.714", readInt32, resolveDnt, writeInt32),
			new AttributeSyntax(typeof(string), "Object(Presentation-Address)", "2.5.5.13", 0x80000 + 13, 127, "1.3.12.2.1011.28.0.732", readUtf16String, null, writeUtf16String),
			new AttributeSyntax(null, "Object(Replica-Link)", "2.5.5.10", 0x80000 + 10, 127, "1.2.840.113556.1.1.1.", readBytes, null, writeBytes),
			// TODO: Just a guess, could not find any examples
			new AttributeSyntax(typeof(string), "String(Case)", "2.5.5.3", 0x80000 + 3, 27, null, readUtf16String, null, writeUtf16String),
			// TODO: Verify
			new AttributeSyntax(typeof(string), "String(IA5)", "2.5.5.5", 0x80000 + 5, 22, null, readBytes, decodeAnsiString, writeBytes),
			// TODO: Update return type once SD decoding is supported
			new AttributeSyntax(typeof(string), "String(NT-Sec-Desc)", "2.5.5.15", 0x80000 + 15, 66, null, readBytes, decodeSD, writeBytes),
			new AttributeSyntax(typeof(byte[]), "String(Numeric)", "2.5.5.6", 0x80000 + 6, 18, null, readBytes, null, writeBytes),
			new AttributeSyntax(typeof(string), "String(Object-Identifier)", "2.5.5.2", 0x80000 + 2, 6, null, readInt32, decodeOid, writeInt32),
			new AttributeSyntax(typeof(byte[]), "String(Octet)", "2.5.5.10", 0x80000 + 10, 4, null, readBytes,null, writeBytes),
			new AttributeSyntax(typeof(byte[]), "String(Printable)", "2.5.5.5", 0x80000 + 5, 19, null, readBytes, null, writeBytes),
			new AttributeSyntax(typeof(SecurityIdentifier), "String(Sid)", "2.5.5.17", 0x80000 + 17, 4, null, readBytes, decodeSid, writeBytes),
			new AttributeSyntax(typeof(string), "String(Teletex)", "2.5.5.4", 0x80000 + 4, 20, null, readUtf16String, null, writeUtf16String),
			new AttributeSyntax(typeof(string), "String(Unicode)", "2.5.5.12", 0x80000 + 12, 64, null, readUtf16String, null, writeUtf16String),
			new AttributeSyntax(typeof(DateTime), "String(UTC-Time)", "2.5.5.11", 0x80000 + 11, 23, null, readInt64, decodeUtcTime, writeInt64),
			new AttributeSyntax(typeof(DateTime), "String(Generalized-Time)", "2.5.5.11", 0x80000 + 11, 24, null, readInt64, decodeGeneralTime, writeInt64),
		};
		this._syntaxes = syntaxes;

		// Special syntaxes
		this._guidSyntax = new AttributeSyntax(typeof(byte[]), "String(Guid)", "2.5.5.10", 0x80000 + 10, 4, null, readBytes, o => new Guid((byte[])o), writeBytes);
		this._instanceTypeSyntax = new AttributeSyntax(typeof(ADInstanceType), "Enumeration(InstanceType)", "2.5.5.9", 0x80000 + 9, 2, null, readInt32, r => (ADInstanceType)(int)r, writeInt32);
		this._systemFlagsSyntax = new AttributeSyntax(typeof(AttributeSystemFlags), "Enumeration(SystemFlags)", "2.5.5.9", 0x80000 + 9, 2, null, readInt32, r => (AttributeSystemFlags)(int)r, writeInt32);
		this._searchFlagsSyntax = new AttributeSyntax(typeof(ADSearchFlags), "Enumeration(SearchFlags)", "2.5.5.9", 0x80000 + 9, 10, null, readInt32, r => (ADSearchFlags)(int)r, writeInt32);

		Dictionary<SyntaxKey, AttributeSyntax> syntaxesByKey = new Dictionary<SyntaxKey, AttributeSyntax>();
		foreach (var syntax in syntaxes)
		{
			syntaxesByKey.Add(new SyntaxKey(syntax.PrefixEncodedId, syntax.OmSyntax), syntax);
		}
		this._syntaxesByKey = syntaxesByKey;
	}

	private static SecurityIdentifier ParseDirectorySid(object r)
	{
		byte[] bytes = (byte[])r;
		// SecurityIdentifier expects the subauthority list in BE format,
		// but NTDS uses LE for the final RID
		int subauthCount = bytes[1];
		ref var rid = ref MemoryMarshal.AsRef<int>(new Span<byte>(bytes, 8 + 4 * subauthCount - 4, 4));
		rid = BinaryPrimitives.ReverseEndianness(rid);
		var sid = new SecurityIdentifier(bytes, 0);
		return sid;
	}

	private object DecodeSD(byte[] r)
	{
		return TryGetSecurityDescriptor(r);
	}

	private object? TryGetSecurityDescriptor(long id)
	{
		var cur = this._sdCursor;
		cur.MakeKey(id, MakeKeyGrbit.NewKey);
		return TryGetSecurityDescriptorFromCursor(cur);
	}

	private object? TryGetSecurityDescriptor(byte[] id)
	{
		var cur = this._sdCursor;
		cur.MakeKey(id, MakeKeyGrbit.NewKey);
		return TryGetSecurityDescriptorFromCursor(cur);
	}

	private object? TryGetSecurityDescriptorFromCursor(JetCursor cur)
	{
		if (cur.Seek())
		{
			byte[] sdBytes = cur.ReadBytes(this._sdValueColid, 1);
			if (sdBytes != null)
			{
				RawSecurityDescriptor sd = new RawSecurityDescriptor(sdBytes, 0);
				string sddl = sd.GetSddlForm(AccessControlSections.All);
				return sddl;
			}
		}

		return null;
	}

	/// <summary>
	/// Gets a class corresponding to an encoded governsID value.
	/// </summary>
	/// <param name="governsId">Prefix-encoded governsID</param>
	/// <returns>An <see cref="NtdsClassSchema"/> with <paramref name="governsId"/></returns>
	/// <exception cref="ArgumentException">No class found with <paramref name="governsId"/></exception>
	public NtdsClassSchema GetClassByGovernsId(int governsId)
		=> this._classesByGovernsId[governsId];
	/// <inheritdoc/>
	IClassSchema IDirectory.GetClassByGovernsId(int governsId) => this.GetClassByGovernsId(governsId);

	/// <summary>
	/// Gets a list of all object classes.
	/// </summary>
	/// <returns>An array of <see cref="NtdsClassSchema"/> objects, one for
	/// each class defined in the directory..</returns>
	public NtdsClassSchema[] GetClasses() => this._classes.ToArray();
	/// <inheritdoc/>
	IClassSchema[] IDirectory.GetClassSchemas() => this.GetClasses();

	/// <summary>
	/// Gets a list of all attribute in the schemae.
	/// </summary>
	/// <returns>An array of <see cref="NtdsAttributeSchema"/> objects.</returns>
	public NtdsAttributeSchema[] GetAllAttributeSchemas() => this._attrs.ToArray();
	/// <inheritdoc/>
	IAttributeSchema[] IDirectory.GetAllAttributeSchemas() => this.GetAllAttributeSchemas();

	/// <summary>
	/// Gets an attribute by ID.
	/// </summary>
	/// <param name="id">Attribute ID</param>
	/// <returns><see cref="NtdsAttributeSchema"/> corresponding to the requested attribute.</returns>
	/// <exception cref="KeyNotFoundException"><paramref name="id"/> is not a valid attribute</exception>
	public NtdsAttributeSchema GetAttributeById(int id)
		=> this._attrsById[id];

	/// <summary>
	/// Gets an attribute by its LDAP name.
	/// </summary>
	/// <param name="name">LDAP name of attribute</param>
	/// <returns>The <see cref="NtdsAttributeSchema"/> with the LDAP name <paramref name="name"/>, if found;
	/// otherwise, <see langword="null"/>.</returns>
	public NtdsAttributeSchema? TryGetAttributeByLdapName(string name)
	{
		if (!string.IsNullOrEmpty(name))
		{
			this._attrsByLdapName.TryGetValue(name, out var attr);
			return attr;
		}
		else
			return null;
	}
	/// <inheritdoc/>
	IAttributeSchema? IDirectory.TryGetAttributeByLdapName(string name) => this.TryGetAttributeByLdapName(name);

	internal NtdsAttributeSchema? TryGetAttributeById(int id)
	{
		this._attrsById.TryGetValue(id, out var attr);
		return attr;
	}
	#endregion

	/// <summary>
	/// Gets the root domain object.
	/// </summary>
	public NtdsDirectoryObject RootDomain { get; }
	/// <inheritdoc/>
	IDirectoryObject IDirectory.RootDomain => this.RootDomain;

	#region Hierarchy and queries
	/// <summary>
	/// Gets an enumerable over the children of parent node.
	/// </summary>
	/// <param name="parentDnt">DNT of parent</param>
	/// <returns>A <see cref="DirectoryEnumerable"/> to enumerate the children</returns>
	public DirectoryEnumerable GetChildrenOf(int parentDnt)
	{
		return new DirectoryEnumerable(this, dir =>
		{
			this.VerifyNotDisposed();
			JetCursor? cursor = null;
			try
			{
				cursor = this.Database.OpenTable(DataTableName);
				cursor.SetIndex(ParentDntNameIndex);

				cursor.MakeKey(parentDnt, MakeKeyGrbit.NewKey | MakeKeyGrbit.FullColumnStartLimit);
				cursor.Seek(SeekGrbit.SeekGE | SeekGrbit.SetIndexRange);
				cursor.MakeKey(parentDnt, MakeKeyGrbit.NewKey | MakeKeyGrbit.FullColumnEndLimit);
				cursor.SetIndexRange(SetIndexRangeGrbit.RangeInclusive | SetIndexRangeGrbit.RangeUpperLimit);

				var result = cursor;
				cursor = null;
				return result;
			}
			finally
			{
				cursor?.Dispose();
			}
		});
	}
	/// <summary>
	/// Gets a <see cref="NtdsDirectoryObject"/> for a DNT.
	/// </summary>
	/// <param name="parentDnt">DNT of object</param>
	/// <returns>A <see cref="NtdsDirectoryObject"/> representing the requested object.</returns>
	/// <exception cref="DirectoryObjectNotFoundException">No object found matching <paramref name="dnt"/>.</exception>
	internal NtdsDirectoryObject GetByDnt(int dnt)
	{
		this.VerifyNotDisposed();

		Debug.Assert(this._dntCursor != null);
		var cursor = this._dntCursor;

		cursor.MakeKey(dnt, MakeKeyGrbit.NewKey);
		if (!cursor.Seek(SeekGrbit.SeekEQ))
			throw new DirectoryObjectNotFoundException();

		return this.GetObjectFromRecord(cursor);
	}

	/// <summary>
	/// Gets a <see cref="NtdsDirectoryObject"/> for a PDNT-name pair.
	/// </summary>
	/// <param name="parentDnt">DNT of parent</param>
	/// <param name="name">Name of object</param>
	/// <returns>A <see cref="NtdsDirectoryObject"/> representing the requested object, if found.</returns>
	internal NtdsDirectoryObject GetByParentDntName(int parentDnt, string name)
	{
		Debug.Assert(!string.IsNullOrEmpty(name));

		this.VerifyNotDisposed();

		using (var cursor = this.Database.OpenTable(DataTableName))
		{
			cursor.SetIndex(ParentDntNameIndex);

			cursor.MakeKey(parentDnt, MakeKeyGrbit.NewKey);
			cursor.MakeKey(name, MakeKeyGrbit.None);
			cursor.Seek(SeekGrbit.SeekEQ);

			return this.GetObjectFromRecord(cursor);
		}
	}


	private Dictionary<int, NtdsDirectoryObject> _cachedObjects = new Dictionary<int, NtdsDirectoryObject>();

	/// <summary>
	/// Gets a <see cref="NtdsDirectoryObject"/> representing the current record of a cursor.o
	/// </summary>
	/// <param name="cursor">Cursor containing data</param>
	/// <returns>A <see cref="NtdsDirectoryObject"/> representing the record.</returns>
	/// <remarks>
	/// The implementation may or may not return the same <see cref="NtdsDirectoryObject"/>
	/// between different calls with cursors with the same record.
	/// </remarks>
	internal NtdsDirectoryObject GetObjectFromRecord(JetCursor cursor)
	{
		Debug.Assert(cursor != null);
		Debug.Assert(cursor.HasCurrentRecord);

		// Get special columns
		var dnt = cursor.ReadInt32(this._specialColumnIds[(int)SpecialColumn.Dnt]).Value;
		{
			if (this._cachedObjects.TryGetValue(dnt, out var cachedObj))
				return cachedObj;
		}

		var pdnt = cursor.ReadInt32(this._specialColumnIds[(int)SpecialColumn.Pdnt]).Value;
		var ancestorBytes = cursor.ReadBytes(this._specialColumnIds[(int)SpecialColumn.Ancestors], 1);
		int[] ancestorDnts;
		{
			if (ancestorBytes != null && ancestorBytes.Length >= 8)
			{
				Span<byte> ancestorsSpan = ancestorBytes;

				var count = ancestorBytes.Length / 4;
				// Chop off the last one, which is the same as the DNT
				count--;
				ancestorDnts = new int[count];
				for (int i = 0; i < count; i++)
				{
					int ancestorDnt = BinaryPrimitives.ReadInt32LittleEndian(ancestorsSpan.Slice(i * 4));
					ancestorDnts[i] = ancestorDnt;
				}
			}
			else
			{
				ancestorDnts = Array.Empty<int>();
			}
		}

		var flags = DirectoryObjectFlags.None;
		if (0 != (cursor.ReadInt32(this._specialColumnIds[(int)SpecialColumn.isDeleted]) ?? 0))
			flags |= DirectoryObjectFlags.IsDeleted;

		var objClassGovernsId = cursor.ReadInt32(this._specialColumnIds[(int)SpecialColumn.objectClass]) ?? 0;
		// TODO: Yeah... #ResearchTool
		var superclassChain = Array.ConvertAll(cursor.ReadMultiInt32(this._specialColumnIds[(int)SpecialColumn.objectClass]), r => r.Value);

		var obj = objClassGovernsId switch
		{
			GovernsIdOfObjClass => new NtdsClassSchema(this, dnt, pdnt),
			AttrIdId => new NtdsAttributeSchema(this, dnt, pdnt),
			_ => new NtdsDirectoryObject(this, dnt, pdnt)
		};
		obj._flags = flags;
		obj._objClassGovernsId = objClassGovernsId;
		obj._superclassChain = superclassChain;
		obj._ancestryBytes = ancestorBytes;
		obj._ancestryDnts = ancestorDnts;
		obj.Name = cursor.ReadUtf16String(this._specialColumnIds[(int)SpecialColumn.Name]);
		obj.InstanceType = (ADInstanceType)(cursor.ReadInt32(this._specialColumnIds[(int)SpecialColumn.instanceType]) ?? 0);
		obj._rdnAttrId = cursor.ReadInt32(this._specialColumnIds[(int)SpecialColumn.RdnType]) ?? 0;

		if (obj is NtdsClassSchema objcls)
		{
			objcls.GovernsIdRaw = cursor.ReadInt32(this._specialColumnIds[(int)SpecialColumn.governsId]) ?? 0;
			objcls.LdapDisplayName = cursor.ReadUtf16String(this._specialColumnIds[(int)SpecialColumn.ldapDisplayName]);
			objcls.AdminDescription = cursor.ReadUtf16String(this._specialColumnIds[(int)SpecialColumn.adminDescription]);
			objcls.SubclassOfId = cursor.ReadInt32(this._specialColumnIds[(int)SpecialColumn.subClassOf]) ?? 0;
			objcls.AuxiliaryClassIds = cursor.ReadNonnullMultiInt32(this._specialColumnIds[(int)SpecialColumn.auxiliaryClass]);
			objcls.SystemAuxiliaryClassIds = cursor.ReadNonnullMultiInt32(this._specialColumnIds[(int)SpecialColumn.systemAuxiliaryClass]);
			objcls.MustContainIds = cursor.ReadNonnullMultiInt32(this._specialColumnIds[(int)SpecialColumn.mustContain]);
			objcls.SystemMustContainIds = cursor.ReadNonnullMultiInt32(this._specialColumnIds[(int)SpecialColumn.systemMustContain]);
			objcls.MayContainIds = cursor.ReadNonnullMultiInt32(this._specialColumnIds[(int)SpecialColumn.mayContain]);
			objcls.SystemMayContainIds = cursor.ReadNonnullMultiInt32(this._specialColumnIds[(int)SpecialColumn.systemMayContain]);

			this._cachedObjects[dnt] = objcls;
		}
		else if (obj is NtdsAttributeSchema attr)
		{
			attr.AttributeIdRaw = cursor.ReadInt32(this._specialColumnIds[(int)SpecialColumn.attributeId]) ?? 0;
			attr.OmSyntax = cursor.ReadInt32(this._specialColumnIds[(int)SpecialColumn.omSyntax]) ?? 0;
			attr.LdapDisplayName = cursor.ReadUtf16String(this._specialColumnIds[(int)SpecialColumn.ldapDisplayName]);
			attr.AdminDescription = cursor.ReadUtf16String(this._specialColumnIds[(int)SpecialColumn.adminDescription]);
			attr.AttributeSyntaxIdPrefixEncoded = cursor.ReadInt32(this._specialColumnIds[(int)SpecialColumn.attributeSyntax]) ?? 0;
			attr.IsSingleValued = 0 != (cursor.ReadInt32(this._specialColumnIds[(int)SpecialColumn.isSingleValued]) ?? 0);
			attr.LinkId = cursor.ReadInt32(this._specialColumnIds[(int)SpecialColumn.linkId]) ?? 0;
			attr.SearchFlags = (ADSearchFlags)(cursor.ReadInt32(this._specialColumnIds[(int)SpecialColumn.searchFlags]) ?? 0);

			char typeChar = (char)('b' + (attr.AttributeSyntaxIdPrefixEncoded - 524289));

			attr.Syntax = GetSyntaxFor(attr);

			try
			{
				if (!attr.IsLink)
				{
					attr.ColumnName = "ATT" + typeChar + attr.AttributeIdRaw;
					attr.ColumnId = this.Database.GetColumnId(DataTableName, attr.ColumnName);
				}
			}
			catch (EsentColumnNotFoundException ex)
			{
				// Column not found, either my algorithm is wrong or the column simply doesn't exist
			}

			this._cachedObjects[dnt] = attr;
		}

		return obj;
	}

	private AttributeSyntax? GetSyntaxFor(NtdsAttributeSchema attr)
	{
		const int ObjectGuidId = 589826;
		const int InstanceTypeId = 131073;
		const int SystemFlagsId = 590199;
		const int SearchFlagsId = 131406;
		const int SchemaGuidId = 589972;
		const int AttributeSecurityGuidId = 589973;
		const int secdescId = 131353;
		if (attr.AttributeIdRaw == ObjectGuidId)
			return this._guidSyntax;
		else if (attr.AttributeIdRaw == InstanceTypeId)
			return this._instanceTypeSyntax;
		else if (attr.AttributeIdRaw == SystemFlagsId)
			return this._systemFlagsSyntax;
		else if (attr.AttributeIdRaw == SearchFlagsId)
			return this._searchFlagsSyntax;
		else if (attr.AttributeIdRaw == SchemaGuidId)
			return this._guidSyntax;
		else if (attr.AttributeIdRaw == AttributeSecurityGuidId)
			return this._guidSyntax;

		else if (this._syntaxesByKey.TryGetValue(new SyntaxKey(attr.AttributeSyntaxIdPrefixEncoded, attr.OmSyntax), out var syntax))
		{
			return syntax;
		}
		return null;
	}
	#endregion

	#region Attribute retrieval
	internal object? GetAttributeValueFor(NtdsDirectoryObject obj, NtdsAttributeSchema attr, int tag, bool decode)
	{
		if (obj is null) throw new ArgumentNullException(nameof(obj));
		if (attr is null) throw new ArgumentNullException(nameof(attr));

		// TODO: Should this default to a different behavior such as returning the value as a byte[] ?
		if (attr.Syntax == null || !attr.Syntax.CanRetrieveValue)
			throw new NotSupportedException();

		if (attr.ExistsInDatabase)
		{
			using (var cursor = this.Database.OpenTable(DataTableName))
			{
				cursor.SetIndex(DntIndexName);

				cursor.MakeKey(obj.Dnt, MakeKeyGrbit.NewKey);
				if (!cursor.Seek(SeekGrbit.SeekEQ))
					throw new DirectoryObjectNotFoundException();

				object? value = RetrieveSingleValueAttribute(attr, tag, cursor, decode);

				return value;
			}
		}
		else if (attr.IsLink)
		{
			throw new NotImplementedException();
		}
		else
			return null;
	}

	private static object? RetrieveSingleValueAttribute(
		NtdsAttributeSchema attr,
		int tag,
		JetCursor cursor,
		bool decode)
	{
		var value = attr.Syntax.retrieveFunc(cursor, attr.ColumnId, tag);
		if (decode && value != null && attr.Syntax.decodeFunc != null)
			value = attr.Syntax.decodeFunc(value);
		return value;
	}

	internal object[] GetAttributeValuesFor(NtdsDirectoryObject obj, IList<NtdsAttributeSchema> attributes, bool decode)
	{
		using (var cursor = this.Database.OpenTable(DataTableName))
		{
			cursor.SetIndex(DntIndexName);

			cursor.MakeKey(obj.Dnt, MakeKeyGrbit.NewKey);
			if (!cursor.Seek(SeekGrbit.SeekEQ))
				throw new DirectoryObjectNotFoundException();

			object?[] values = new object[attributes.Count];
			for (int i = 0; i < values.Length; i++)
			{
				NtdsAttributeSchema? attr = attributes[i];
				if (attr == null)
					continue;

				object? value = null;
				// TODO: Should this default to a different behavior such as returning the value as a byte[] ?
				if (attr.IsLink)
				{
					value = this.GetLinkValues(obj, attr);
				}
				else if (attr.ExistsInDatabase && attr.Syntax != null && attr.Syntax.CanRetrieveValue)
				{
					value = attr.IsSingleValued ? RetrieveSingleValueAttribute(attr, 1, cursor, decode) : RetrieveMultiValueAttribute(attr, cursor);
				}
				else
				{
				}

				values[i] = value;
			}


			return values;
		}
	}

	/// <summary>
	/// Gets all values for a multi-value attribute
	/// </summary>
	/// <param name="obj">Object to get values for</param>
	/// <param name="attribute">Attribute to get values for</param>
	/// <returns></returns>
	/// <exception cref="ArgumentNullException"><paramref name="obj"/> is <see langword="null"/></exception>
	/// <exception cref="ArgumentNullException"><paramref name="attribute"/> is <see langword="null"/></exception>
	/// <exception cref="ArgumentException"><paramref name="attribute"/> is a single-valued attribute.</exception>
	/// <exception cref="NotSupportedException"><paramref name="attribute"/> uses an unsupported syntax.</exception>
	/// <exception cref="DirectoryObjectNotFoundException"><paramref name="obj"/> doesn't exist in the directory</exception>
	internal MultiValue? GetAttributeMultiValuesFor(NtdsDirectoryObject obj, NtdsAttributeSchema attribute)
	{
		if (obj is null) throw new ArgumentNullException(nameof(obj));
		if (attribute is null) throw new ArgumentNullException(nameof(attribute));
		if (attribute.IsSingleValued) throw new ArgumentException(Messages.DirectoryObject_AttrIsSingleValued, nameof(attribute));

		Debug.Assert(attribute.Directory == this);
		Debug.Assert(obj.Directory == this);

		if (attribute.Syntax == null)
			throw new NotSupportedException(Messages.Attribute_SyntaxNotSupported);
		// UNDONE: Indicated by null return value
		//if (!attribute.Syntax.CanRetrieveValue)
		//	throw new NotSupportedException(Messages.Attribute_NotInDatabase);

		if (attribute.ExistsInDatabase)
		{
			var cursor = this._dntCursor;
			cursor.MakeKey(obj.Dnt, MakeKeyGrbit.NewKey);
			if (!cursor.Seek(SeekGrbit.SeekEQ))
				throw new DirectoryObjectNotFoundException();

			return RetrieveMultiValueAttribute(attribute, cursor);
		}
		else if (attribute.IsLink)
		{
			return GetLinkValues(obj, attribute);
		}
		else
		{
			return null;
		}
	}

	private MultiValue? GetLinkValues(NtdsDirectoryObject obj, NtdsAttributeSchema attribute)
	{
		bool isBacklink = (attribute.LinkId % 2) != 0;
		using (var cursor = this.Database.OpenTable(LinkTableName))
		{
			cursor.SetIndex(isBacklink ? BacklinkIndexName : LinkIndexName);
			cursor.MakeKey(obj.Dnt, MakeKeyGrbit.NewKey);
			cursor.MakeKey(attribute.LinkId / 2, MakeKeyGrbit.FullColumnStartLimit);
			if (cursor.Seek(SeekGrbit.SeekGE | SeekGrbit.SetIndexRange))
			{
				cursor.MakeKey(obj.Dnt, MakeKeyGrbit.NewKey);
				cursor.MakeKey(attribute.LinkId / 2, MakeKeyGrbit.FullColumnEndLimit);
				if (cursor.SetIndexRange(SetIndexRangeGrbit.RangeInclusive | SetIndexRangeGrbit.RangeUpperLimit))
				{
					var colid = linkTableColIds[isBacklink ? 2 : 0];
					List<NtdsDirectoryObjectReference> objrefs = new List<NtdsDirectoryObjectReference>();
					do
					{
						var targetDnt = cursor.ReadInt32(colid) ?? 0;
						var objref = new NtdsDirectoryObjectReference(this, targetDnt);
						objrefs.Add(objref);
					} while (cursor.MoveNext());

					var multi = new MultiValue<NtdsDirectoryObjectReference>(objrefs.ToArray(), objrefs.Count);
					return multi;
				}
			}
		}
		return null;
	}

	private static MultiValue? RetrieveMultiValueAttribute(NtdsAttributeSchema attribute, JetCursor cursor)
	{
		var count = cursor.GetValueCount(attribute.ColumnId);
		if (count > 0)
		{
			Array array = Array.CreateInstance(attribute.Syntax.AttributeType, count);
			int validCount = 0;
			for (int i = 0; i < count; i++)
			{
				var value = attribute.Syntax.retrieveFunc(cursor, attribute.ColumnId, i + 1);
				if (value != null)
				{
					if (attribute.Syntax.decodeFunc != null)
						value = attribute.Syntax.decodeFunc(value);

					array.SetValue(value, validCount);
					validCount++;
				}
			}

			return MultiValue.Create(array, validCount);
		}
		return null;
	}

	internal void SetAttributeRawValueFor(
		NtdsDirectoryObject obj,
		NtdsAttributeSchema attribute,
		object? value)
	{
		if (attribute.Syntax == null)
			throw new NotSupportedException($"Cannot set attribute {attribute.Name} because the syntax is not supported.");

	}

	internal void SetParentDntOf(NtdsDirectoryObject obj, int dnt)
	{
		var cur = this._dntCursor;
		try
		{
			cur.MakeKey(obj.Dnt, MakeKeyGrbit.NewKey);
			if (cur.Seek())
			{
				cur.SetInt32(this._specialColumnIds[(int)SpecialColumn.Pdnt], 1, dnt);
				cur.ApplyUpdate();
			}
		}
		finally
		{
			cur.CancelUpdate();
		}
	}

	internal IEnumerable<NtdsDirectoryObject> SearchSubtree(
		NtdsDirectoryObject searchRoot,
		string? searchName,
		NtdsClassSchema? classSchema,
		bool includeSubclasses)
	{
		var direnum = this.SearchSubtree(searchRoot);
		bool Matches(object? attrValue, string searchValue)
		{
			var str = attrValue?.ToString();
			return (str != null) && (str.Contains(searchValue, StringComparison.OrdinalIgnoreCase));
		}

		var matchAllNames = string.IsNullOrEmpty(searchName);

		foreach (var obj in direnum)
		{
			bool classMatches =
				(classSchema is null)
				|| (includeSubclasses ? obj.IsInstanceOf(classSchema)
					: (obj.ObjectClass == classSchema));
			if (!classMatches)
				continue;

			bool anyValueMatches = false;

			// TODO: This could be made more efficient by walking through the cursor
			// rather than instantiating a DirectoryObject for every record

			// TODO: Intersect with index on ATTc0 for object class

			var attrValues = obj.GetValueOfMultiple(this._anrAttrs);
			foreach (var attrValue in attrValues)
			{
				if (attrValue is null)
					continue;

				if (attrValue is MultiValue multi)
				{
					foreach (var value in multi)
					{
						if (matchAllNames || Matches(value, searchName))
						{
							anyValueMatches = true;
							break;
						}
					}
				}
				else
				{
					anyValueMatches = matchAllNames || Matches(attrValue, searchName);
				}

				if (anyValueMatches)
					break;
			}

			if (anyValueMatches)
				yield return obj;
		}
	}

	internal DirectoryEnumerable SearchSubtree(NtdsDirectoryObject searchRoot)
	{
		var enumerable = new DirectoryEnumerable(this, dir =>
		{
			this.VerifyNotDisposed();

			var curAncestry = this.Database.OpenTable(DataTableName);
			curAncestry.SetIndex(AncestorsIndex);
			curAncestry.MakeKey(searchRoot._ancestryBytes, MakeKeyGrbit.NewKey);
			var hasrec = curAncestry.Seek(SeekGrbit.SeekGE | SeekGrbit.SetIndexRange);

			byte[] limit = (byte[])searchRoot._ancestryBytes.Clone();
			limit[^1]++;
			curAncestry.MakeKey(limit, MakeKeyGrbit.NewKey);
			hasrec = curAncestry.SetIndexRange(SetIndexRangeGrbit.RangeUpperLimit);

			return curAncestry;
		});
		return enumerable;
	}
	#endregion

	#region Membership
	internal IEnumerable<IDirectoryObject> GetMembersOf(NtdsDirectoryObject group)
	{
		Debug.Assert(group.Directory == this);

		// Get RID
		var sidBytes = group.GetValueOf(this.GetAttributeById(ObjectSidAttrId), 1, false) as byte[];
		if (sidBytes == null || sidBytes.Length == 0)
			return Array.Empty<IDirectoryObject>();
		var rid = BinaryPrimitives.ReadInt32BigEndian(sidBytes[^4..^0]);

		// Links
		var atrMember = this.GetAttributeById(MemberAttrId);
		var sourceDnt = group.Dnt;

		return new DirectoryEnumerable(this, dir =>
		{
			// Search by primaryGroupID

			this.VerifyNotDisposed();

			var cur = this.Database.OpenTable(DataTableName);
			cur.SetIndex(PrimaryGroupIdIndex);
			cur.MakeKey(rid, MakeKeyGrbit.NewKey);
			var hasrec = cur.Seek(SeekGrbit.SeekGE | SeekGrbit.SetIndexRange);
			if (hasrec)
			{
				cur.MakeKey(rid, MakeKeyGrbit.NewKey);
				hasrec = cur.SetIndexRange(SetIndexRangeGrbit.RangeUpperLimit | SetIndexRangeGrbit.RangeInclusive);
			}
			return cur;
		}).Concat(GetLinkEnumerable(atrMember, sourceDnt));
	}
	internal IEnumerable<IDirectoryObject> GetMemberOfGroups(NtdsDirectoryObject member)
	{
		Debug.Assert(member.Directory == this);

		// Links
		var atrMember = this.GetAttributeById(MemberOfAttrId);
		var sourceDnt = member.Dnt;

		IEnumerable<IDirectoryObject> links = GetLinkEnumerable(atrMember, sourceDnt);
		// Get primary group ID
		var groupId = member.GetValueOf(this.GetAttributeById(PrimaryGroupIdAttrId), 1, false) as int?;
		if (groupId.HasValue)
		{
			var sidBytes = member.GetValueOf(this.GetAttributeById(ObjectSidAttrId), 1, false) as byte[];
			if (sidBytes == null || sidBytes.Length == 0)
				return Array.Empty<IDirectoryObject>();

			BinaryPrimitives.WriteInt32BigEndian(new Span<byte>(sidBytes).Slice(sidBytes.Length - 4), groupId.Value);

			using (var cur = this.Database.OpenTable(DataTableName))
			{
				cur.SetIndex(ObjectSidIndex);
				cur.MakeKey(sidBytes, MakeKeyGrbit.NewKey);
				if (cur.Seek(SeekGrbit.SeekEQ))
				{
					NtdsDirectoryObject primaryGroup = this.GetObjectFromRecord(cur);
					links = links.Concat(new NtdsDirectoryObject[] { primaryGroup });
				}
			}
		}

		return links;
	}

	private NtdsDirectoryLinkEnumerable GetLinkEnumerable(
		NtdsAttributeSchema atrMember,
		int sourceDnt)
	{
		var linkId = atrMember.LinkId;
		bool isBacklink = (atrMember.LinkId % 2) != 0;
		var targetDntColid = linkTableColIds[isBacklink ? 2 : 0];
		var linkBase = linkId / 2;
		return new NtdsDirectoryLinkEnumerable(this, dir =>
		{
			this.VerifyNotDisposed();

			var cursor = this.Database.OpenTable(LinkTableName);
			cursor.SetIndex(isBacklink ? BacklinkIndexName : LinkIndexName);
			cursor.MakeKey(sourceDnt, MakeKeyGrbit.NewKey);
			cursor.MakeKey(linkBase, MakeKeyGrbit.FullColumnStartLimit);
			cursor.Seek(SeekGrbit.SeekGE | SeekGrbit.SetIndexRange);

			cursor.MakeKey(sourceDnt, MakeKeyGrbit.NewKey);
			cursor.MakeKey(linkBase, MakeKeyGrbit.FullColumnEndLimit);
			cursor.SetIndexRange(SetIndexRangeGrbit.RangeInclusive | SetIndexRangeGrbit.RangeUpperLimit);

			return cursor;
		}, targetDntColid);
	}
	#endregion
}

partial class NtdsDirectory : IDisposable
{
	private bool disposedValue;
	protected void VerifyNotDisposed()
	{
		if (this.disposedValue)
			throw new ObjectDisposedException(Messages.ObjectDisposedMessage);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
				this._dntCursor?.Dispose();
				this._sdCursor?.Dispose();
				this.Database?.Dispose();
				this.session?.Dispose();
				this.instance?.Dispose();
			}

			disposedValue = true;
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
