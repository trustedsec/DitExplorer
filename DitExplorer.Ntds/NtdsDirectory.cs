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
	private const string GuidIndexName = "INDEX_00090002";
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

	private const int DomainGovernsId = 655427;
	private const string ObjectClassIndex = "INDEX_00000000";

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
			NtdsDirectoryObject? rootDomainObj = null;
			using (var cur = this.Database.OpenTable(DataTableName))
			{
				cur.SetIndex(ObjectClassIndex);
				cur.MakeKey(DomainGovernsId, MakeKeyGrbit.NewKey);
				cur.Seek(SeekGrbit.SeekEQ);
				cur.MakeKey(DomainGovernsId, MakeKeyGrbit.NewKey);
				cur.SetIndexRange(SetIndexRangeGrbit.RangeInclusive | SetIndexRangeGrbit.RangeUpperLimit);

				while (cur.HasCurrentRecord)
				{
					var obj = this.GetObjectFromRecord(cur);
					rootDomainObj = obj;
					break;

					cur.MoveNext();
				}
			}

			// No root domain provided, try to detect the root domain
			// Start at the root of the database and return the first NC head
			var rootObj = this.GetChildrenOf(0).FirstOrDefault();
			this.RootObject = rootObj;
			//if (rootObj != null)
			//{
			//	int dnt = rootObj.Dnt;
			//	do
			//	{
			//		bool foundChild = false;
			//		foreach (var obj in this.GetChildrenOf(dnt))
			//		{
			//			// Skip deleted objects
			//			if (obj.IsDeleted)
			//				continue;

			//			foundChild = true;
			//			if (obj.IsNCHead)
			//			{
			//				// NC head, assume it's the root domain
			//				rootObj = obj;
			//			}
			//			else
			//				// Nope, keep going
			//				dnt = obj.Dnt;

			//			break;
			//		}
			//		if (!foundChild)
			//			break;
			//	} while (rootDomainObj == null);
			//}

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

		const int ConfGovernsId = 655372;
		const int SchemaGovernsId = 196617;
		NtdsDirectoryObject schemaContainer;
		using (var cursor = this.Database.OpenTable(DataTableName))
		{
			cursor.SetIndex(ObjectClassIndex);
			cursor.MakeKey(SchemaGovernsId, MakeKeyGrbit.NewKey);
			if (!cursor.Seek(SeekGrbit.SeekEQ))
				throw new InvalidDataException(Messages.Error_SchemaContainerNotFound);
			schemaContainer = this.GetObjectFromRecord(cursor);
		}

		foreach (var item in schemaContainer.GetChildren())
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
		"1.2.840.113556.1.2",
		"1.2.840.113556.1.3",
		"2.16.840.1.101.2.2.1",
		"2.16.840.1.101.2.2.3",
		"2.16.840.1.101.2.1.5",
		"2.16.840.1.101.2.1.4",
		"2.5.5",
		"1.2.840.113556.1.4",
		// 10
		"1.2.840.113556.1.5",
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		null,
		"0.9.2342.19200300.100",
		// 20
		"2.16.840.1.113730.3",
		"0.9.2342.19200300.100.1",
		"2.16.840.1.113730.3.1",
		"1.2.840.113556.1.5.7000",
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
	internal string DecodePrefixedOid(int encoded)
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

	internal int EncodeOid(string oid)
	{
		if (string.IsNullOrEmpty(oid)) throw new ArgumentException($"'{nameof(oid)}' cannot be null or empty.", nameof(oid));
		int sep = oid.LastIndexOf('.');
		if (sep == -1)
			throw new ArgumentException("The string is not a valid OID.", nameof(oid));

		string prefix = oid.Substring(0, sep);
		int index = Array.IndexOf(OidPrefixes, prefix);
		if (index == -1)
			throw new ArgumentException("The OID prefix is not recognized.", nameof(oid));

		return (index << 16) | int.Parse(oid.AsSpan().Slice(sep + 1));
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
	private AttributeSyntax _schemaFlagsExSyntax;
	private AttributeSyntax _groupTypeSyntax;
	private AttributeSyntax _uacSyntax;
	private AttributeSyntax _samAccountTypeSyntax;
	private AttributeSyntax _utcTimeSyntax;
	private AttributeSyntax _fileTimeSyntax;
	private AttributeSyntax _localTimeSyntax;
	private AttributeSyntax _int64TimeSyntax;

	/// <summary>
	/// Creates the attribute syntaxes used by attributes.
	/// </summary>
	/// <remarks>
	/// This is based on the table in <see href="https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-adts/7cda533e-d7a4-4aec-a517-91d02ff4a1aa">[MS-ADTS] § 3.1.1.2.2.2 - LDAP Representations</see>.
	/// </remarks>
	private void LoadSyntaxes()
	{
		// TODO: In general, verify attribute decoding and fill out this table
		var syntaxes = new AttributeSyntax[]
		{
			new BoolAttributeSyntax(new AttrSyntaxInfo("Boolean", "2.5.5.8", 0x80000 + 8, 1)),
			new Int32AttributeSyntax(new AttrSyntaxInfo("Enumeration", "2.5.5.9", 0x80000 + 9, 10)),
			new Int32AttributeSyntax(new AttrSyntaxInfo("Integer", "2.5.5.9", 0x80000 + 9, 2)),
			this._int64TimeSyntax = new Int64AttributeSyntax(new AttrSyntaxInfo("LargeInteger", "2.5.5.16", 0x80000 + 16, 65)),
			// TODO: No examples found, could be UTF-16 string or binary
			//new AttributeSyntax("Object(Access-Point)", "2.5.5.14", 0x80000 + 14, 127, "1.3.12.2.1011.28.0.702", null, null),
			// TODO: No examples found, could be UTF-16 string or binary
			new BinaryAttributeSyntax(new AttrSyntaxInfo("Object(DN-String)", "2.5.5.14", 0x80000 + 14, 127, "1.2.840.113556.1.1.1.12")),
			//new AttributeSyntax("Object(OR-Name)", "2.5.5.7", 0x80000 + 7, 127, "2.6.6.1.2.5.11.29", readBytes, null),
			new ObjRefAttributeSyntax(new AttrSyntaxInfo("Object(DN-Binary)", "2.5.5.7", 0x80000 + 7, 127, "1.2.840.113556.1.1.1.11"), this),
			// TODO: Decoder
			new DntAttributeSyntax(new AttrSyntaxInfo("Object(DS-DN)", "2.5.5.1", 0x80000 + 1, 127, "1.3.12.2.1011.28.0.714"), this),
			new BinaryAttributeSyntax(new AttrSyntaxInfo("Object(Presentation-Address)", "2.5.5.13", 0x80000 + 13, 127, "1.3.12.2.1011.28.0.732")),
			new BinaryAttributeSyntax(new AttrSyntaxInfo( "Object(Replica-Link)", "2.5.5.10", 0x80000 + 10, 127, "1.2.840.113556.1.1.1.")),
			// TODO: Just a guess, could not find any examples
			new Utf16StringAttributeSyntax(new AttrSyntaxInfo("String(Case)", "2.5.5.3", 0x80000 + 3, 27)),
			// TODO: Verify
			new AsciiStringAttributeSyntax(new AttrSyntaxInfo( "String(IA5)", "2.5.5.5", 0x80000 + 5, 22)),
			// TODO: Update return type once SD decoding is supported
			new SecDescAttributeSyntax(new AttrSyntaxInfo("String(NT-Sec-Desc)", "2.5.5.15", 0x80000 + 15, 66), this),
			new BinaryAttributeSyntax(new AttrSyntaxInfo( "String(Numeric)", "2.5.5.6", 0x80000 + 6, 18)),
			new OidAttributeSyntax(new AttrSyntaxInfo("String(Object-Identifier)", "2.5.5.2", 0x80000 + 2, 6), this),
			new BinaryAttributeSyntax(new AttrSyntaxInfo("String(Octet)", "2.5.5.10", 0x80000 + 10, 4)),
			new Utf16StringAttributeSyntax(new AttrSyntaxInfo("String(Printable)", "2.5.5.5", 0x80000 + 5, 19)),
			new SidAttributeSyntax(new AttrSyntaxInfo("String(Sid)", "2.5.5.17", 0x80000 + 17, 4)),
			new Utf16StringAttributeSyntax(new AttrSyntaxInfo("String(Teletex)", "2.5.5.4", 0x80000 + 4, 20)),
			new Utf16StringAttributeSyntax(new AttrSyntaxInfo("String(Unicode)", "2.5.5.12", 0x80000 + 12, 64)),
			this._utcTimeSyntax = new DateTimeAttributeSyntax(new AttrSyntaxInfo("String(UTC-Time)", "2.5.5.11", 0x80000 + 11, 23), false),
			this._localTimeSyntax = new DateTimeAttributeSyntax(new AttrSyntaxInfo("String(Generalized-Time)", "2.5.5.11", 0x80000 + 11, 24), true),
		};
		this._syntaxes = syntaxes;

		// Special syntaxes
		this._fileTimeSyntax = new FileTimeAttributeSyntax(new AttrSyntaxInfo("Interval", "2.5.5.9", 0x80000 + 9, 65), false);
		this._guidSyntax = new GuidAttributeSyntax(new AttrSyntaxInfo("String(Guid)", "2.5.5.10", 0x80000 + 10, 4));
		this._instanceTypeSyntax = new Int32EnumAttributeSyntax<ADInstanceType>(new AttrSyntaxInfo("Enumeration(InstanceType)", "2.5.5.9", 0x80000 + 9, 2));
		this._systemFlagsSyntax = new Int32EnumAttributeSyntax<AttributeSystemFlags>(new AttrSyntaxInfo("Enumeration(SystemFlags)", "2.5.5.9", 0x80000 + 9, 2));
		this._searchFlagsSyntax = new Int32EnumAttributeSyntax<ADSearchFlags>(new AttrSyntaxInfo("Enumeration(SearchFlags)", "2.5.5.9", 0x80000 + 9, 10));
		this._schemaFlagsExSyntax = new Int32EnumAttributeSyntax<ADSchemaFlagsEx>(new AttrSyntaxInfo("Enumeration(SchemaFlagsEx)", "2.5.5.9", 0x80000 + 9, 10));

		// TODO: Verify OM-Syntax value
		this._groupTypeSyntax = new Int32EnumAttributeSyntax<ADGroupType>(new AttrSyntaxInfo("Enumeration(GroupType)", "2.5.5.9", 0x80000 + 9, 10));
		this._uacSyntax = new Int32EnumAttributeSyntax<ADUserAccountControlFlags>(new AttrSyntaxInfo("Enumeration(UserAccountControl)", "2.5.5.9", 0x80000 + 9, 10));
		this._samAccountTypeSyntax = new Int32EnumAttributeSyntax<ADSamAccountType>(new AttrSyntaxInfo("Enumeration(SchemaFlagsEx)", "2.5.5.9", 0x80000 + 9, 10));

		Dictionary<SyntaxKey, AttributeSyntax> syntaxesByKey = new Dictionary<SyntaxKey, AttributeSyntax>();
		foreach (var syntax in syntaxes)
		{
			syntaxesByKey.Add(new SyntaxKey(syntax.PrefixEncodedId, syntax.OmSyntax), syntax);
		}
		this._syntaxesByKey = syntaxesByKey;
	}

	internal static SecurityIdentifier DecodeDirectorySid(byte[] r)
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

	internal object? TryGetSecurityDescriptor(long id)
	{
		var cur = this._sdCursor;
		cur.MakeKey(id, MakeKeyGrbit.NewKey);
		return TryGetSecurityDescriptorFromCursor(cur);
	}

	internal object? TryGetSecurityDescriptor(byte[] id)
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
	internal NtdsClassSchema? TryGetClassByGovernsId(int governsId)
	{
		this._classesByGovernsId.TryGetValue(governsId, out var obj);
		return obj;
	}
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

	public NtdsClassSchema? TryGetClassByLdapName(string ldapName)
	{
		this._classesByLdapName.TryGetValue(ldapName, out var obj);
		return obj;
	}

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
	/// <summary>
	/// Gets the object at the root of the directory tree.
	/// </summary>
	public NtdsDirectoryObject RootObject { get; }
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
	public NtdsDirectoryObject GetByDnt(int dnt)
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
	/// Gets an object by its distinguished name.
	/// </summary>
	/// <param name="dn">DN of object</param>
	/// <returns>The object identified by <paramref name="dn"/>.</returns>
	public NtdsDirectoryObject GetObjectByDn(DistinguishedName dn)
	{
		if (dn is null) throw new ArgumentNullException(nameof(dn));

		var parent = this.RootObject;
		for (int i = dn.Parts.Length - 1; i >= 0; i--)
		{
			var part = dn.Parts[i];
			var child = parent.GetChild(part.Value);
			parent = child;
		}

		return parent;
	}
	/// <inheritdoc/>
	IDirectoryObject IDirectory.GetObjectByDn(DistinguishedName dn)
		=> this.GetObjectByDn(dn);
	/// <summary>
	/// Gets an object by its distinguished name.
	/// </summary>
	/// <param name="dn">DN of object</param>
	/// <returns>The object identified by <paramref name="dn"/>.</returns>
	public NtdsDirectoryObject GetObjectByDn(string dn)
	{
		if (string.IsNullOrEmpty(dn)) throw new ArgumentException($"'{nameof(dn)}' cannot be null or empty.", nameof(dn));
		if (dn.StartsWith('<') && dn.EndsWith('>'))
		{
			if (dn.StartsWith("<SID=", StringComparison.OrdinalIgnoreCase))
			{
				var sidstr = dn.Substring(5, dn.Length - 6);
				return this.GetObjectBySid(new SecurityIdentifier(sidstr));
			}
			else if (dn.StartsWith("<GUID="))
			{
				var guidstr = dn.Substring(6, dn.Length - 7);
				return this.GetObjectByGuid(Guid.Parse(guidstr));
			}
			else if (dn.StartsWith("<DNT="))
			{
				var dntstr = dn.Substring(5, dn.Length - 6);
				return this.GetByDnt(int.Parse(dntstr));
			}
			else
			{
				throw new ArgumentException(Messages.NtdsDirectory_BadExtendedDn, nameof(dn));
			}
		}

		return this.GetObjectByDn(DistinguishedName.Parse(dn));
	}
	/// <inheritdoc/>
	IDirectoryObject IDirectory.GetObjectByDn(string dn)
		=> this.GetObjectByDn(dn);
	/// <summary>
	/// Gets an object with the specified GUID.
	/// </summary>
	/// <param name="guid">GUID to search for</param>
	/// <returns>The <see cref="IDirectoryObject"/> with objectGuid equal to <paramref name="guid"/>.</returns>
	public NtdsDirectoryObject GetObjectByGuid(Guid guid)
	{
		this.VerifyNotDisposed();

		using (var cursor = this.Database.OpenTable(DataTableName))
		{
			cursor.SetIndex(GuidIndexName);
			cursor.MakeKey(guid, MakeKeyGrbit.NewKey);
			if (!cursor.Seek(SeekGrbit.SeekEQ))
				throw new DirectoryObjectNotFoundException();
			return this.GetObjectFromRecord(cursor);
		}
	}
	IDirectoryObject IDirectory.GetObjectByGuid(Guid guid) => this.GetObjectByGuid(guid);

	/// <summary>
	/// Gets an object with the specified SID.
	/// </summary>
	/// <param name="sid">SID to search for</param>
	/// <returns>The <see cref="IDirectoryObject"/> with objectSid equal to <paramref name="sid"/>.</returns>
	public NtdsDirectoryObject GetObjectBySid(SecurityIdentifier sid)
	{
		this.VerifyNotDisposed();

		using (var cursor = this.Database.OpenTable(DataTableName))
		{
			byte[] bytes = EncodeSid(sid);

			cursor.SetIndex(ObjectSidIndex);
			cursor.MakeKey(bytes, MakeKeyGrbit.NewKey);
			if (!cursor.Seek(SeekGrbit.SeekEQ))
				throw new DirectoryObjectNotFoundException();
			return this.GetObjectFromRecord(cursor);
		}
	}

	internal static byte[] EncodeSid(SecurityIdentifier sid)
	{
		byte[] bytes = new byte[sid.BinaryLength];
		sid.GetBinaryForm(bytes, 0);
		Span<byte> ridSpan = bytes.AsSpan(bytes.Length - 4);
		BinaryPrimitives.WriteUInt32BigEndian(ridSpan, BinaryPrimitives.ReadUInt32LittleEndian(ridSpan));
		return bytes;
	}

	IDirectoryObject IDirectory.GetObjectBySid(SecurityIdentifier sid) => this.GetObjectBySid(sid);

	/// <summary>
	/// Gets a <see cref="NtdsDirectoryObject"/> for a PDNT-name pair.
	/// </summary>
	/// <param name="parentDnt">DNT of parent</param>
	/// <param name="name">Name of object</param>
	/// <returns>A <see cref="NtdsDirectoryObject"/> representing the requested object, if found.</returns>
	public NtdsDirectoryObject GetByParentDntName(int parentDnt, string name)
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

	private static readonly string[] timestampAttrNames = new string[]
	{
		// Unverified
		"accountExpires",
		"lockoutTime",
		"builtinCreationTime",
		"lSACreationTime",
		"msKds-UseStartTime",
	};
	private static readonly string[] fileTimeAttrNames = new string[]
	{
		// Verified
		"creationTime",
		"pwdLastSet",
		"lastLogon",
		"lastLogonTimestamp",
		"lastSetTime",
		"badPasswordTime",
	};
	private AttributeSyntax? GetSyntaxFor(NtdsAttributeSchema attr)
	{
		const int ObjectGuidId = 589826;
		const int InstanceTypeId = 131073;
		const int SystemFlagsId = 590199;
		const int SearchFlagsId = 131406;
		const int SchemaGuidId = 589972;
		const int AttributeSecurityGuidId = 589973;

		const int SchemaFlagsExId = 589944;
		const int SamAccountTypeId = 590126;
		const int GroupTypeId = 590574;
		const int UserAccountControlId = 589832;

		if (attr.AttributeIdRaw == ObjectGuidId)
			return this._guidSyntax;
		else if (attr.AttributeIdRaw == InstanceTypeId)
			return this._instanceTypeSyntax;
		else if (attr.AttributeIdRaw == SystemFlagsId)
			return this._systemFlagsSyntax;
		else if (attr.AttributeIdRaw == SearchFlagsId)
			return this._searchFlagsSyntax;
		else if (attr.AttributeIdRaw == SchemaFlagsExId)
			return this._schemaFlagsExSyntax;
		else if (attr.AttributeIdRaw == SamAccountTypeId)
			return this._samAccountTypeSyntax;
		else if (attr.AttributeIdRaw == GroupTypeId)
			return this._groupTypeSyntax;
		else if (attr.AttributeIdRaw == UserAccountControlId)
			return this._uacSyntax;

		else if (attr.AttributeIdRaw == SchemaGuidId)
			return this._guidSyntax;
		else if (attr.AttributeIdRaw == AttributeSecurityGuidId)
			return this._guidSyntax;
		else if (this._syntaxesByKey.TryGetValue(new SyntaxKey(attr.AttributeSyntaxIdPrefixEncoded, attr.OmSyntax), out var syntax))
		{
			if (Array.IndexOf(timestampAttrNames, attr.LdapDisplayName) >= 0)
				syntax = this._utcTimeSyntax;
			if (Array.IndexOf(fileTimeAttrNames, attr.LdapDisplayName) >= 0)
				syntax = this._fileTimeSyntax;
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
		var value = attr.Syntax.RetrieveValueFrom(cursor, attr.ColumnId, tag);
		if (decode && value != null)
			value = attr.Syntax.DecodeValue(value);
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

				try
				{
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
				catch
				{
					// TODO: Throw exception to indicate partial success
				}
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
			Array array = Array.CreateInstance(attribute.Syntax.AttributeType ?? typeof(object), count);
			int validCount = 0;
			for (int i = 0; i < count; i++)
			{
				var value = attribute.Syntax.RetrieveValueFrom(cursor, attribute.ColumnId, i + 1);
				if (value != null)
				{
					value = attribute.Syntax.DecodeValue(value);

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

	internal IEnumerable<IDirectoryObject> SearchSubtree(NtdsDirectoryObject searchRoot, DirectoryQuery query)
	{
		Debug.Assert(searchRoot != null);
		Debug.Assert(query != null);

		// TODO: Use indexes
		// For now, take the naive approach of filtering the entire subtree
		var subtreeItems = this.SearchSubtree(searchRoot);
		foreach (var item in subtreeItems)
		{
			bool matches = false;
			try
			{
				matches = (query.Filter.TestObject(item));
			}
			catch
			{
				// TODO: Collect errors and throw exceptions
			}
			if (matches)
				yield return item;
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
		var sidBytes = (group.GetValueOf(this.GetAttributeById(ObjectSidAttrId), 1, false) as byte[]);
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
			var sidBytes = (member.GetValueOf(this.GetAttributeById(ObjectSidAttrId), 1, false) as byte[]);
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
