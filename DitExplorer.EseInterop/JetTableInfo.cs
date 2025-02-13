using Microsoft.Isam.Esent.Interop;

namespace DitExplorer.EseInterop;

/// <summary>
/// Represents a table within an ESE database.
/// </summary>
public class JetTableInfo
{
	private JetDatabase _db;

	internal JetTableInfo(string name, JetDatabase db, JET_OBJECTINFO objInfo)
		:this(name,db,
			 recordCount: objInfo.cRecord,
			 pagesUsed: objInfo.cPage,
			 flags: objInfo.flags
			 )
	{
	}

	internal JetTableInfo(string name, JetDatabase db,
		int recordCount,
		int pagesUsed,
		ObjectInfoFlags flags
		)
	{
		this.TableName = name;
		this._db = db;
		this.RecordCount = recordCount;
		this.PagesUsed = pagesUsed;
		this.Flags = flags;
	}

	/// <summary>
	/// Gets the name of the table.
	/// </summary>
	public string TableName { get; }

	/// <summary>
	/// Gets the number of records in the table.
	/// </summary>
	public int RecordCount { get; }

	/// <summary>
	/// Gets the number of pages used by the table.
	/// </summary>
	public int PagesUsed { get; }

	/// <summary>
	/// Gets a <see cref="ObjectInfoFlags"/> applied to the object.
	/// </summary>
	public ObjectInfoFlags Flags { get; }

	public JET_COLUMNID GetColumnId(string columnName)
	{
		return _db.GetColumnId(TableName, columnName);
	}

	public JetColumnInfo GetColumnInfo(string columnName)
	{
		return _db.GetColumnInfo(TableName, columnName);
	}
}