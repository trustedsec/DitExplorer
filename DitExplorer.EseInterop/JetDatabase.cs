using DitExplorer.EseInterop;
using Microsoft.Isam.Esent.Interop;
using Microsoft.Isam.Esent.Interop.Windows8;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.EseInterop;

/// <summary>
/// Represents an ESE database.
/// </summary>
public partial class JetDatabase
{
	private readonly JET_SESID sesid;
	private readonly JET_DBID dbid;

	internal JetDatabase(JET_SESID sesid, JET_DBID dbid, string fileName)
	{
		this.sesid = sesid;
		this.dbid = dbid;
		this.FileName = fileName;
	}

	/// <summary>
	/// Gets the database file name.
	/// </summary>
	public string FileName { get; }

	/// <summary>
	/// Opens a table.
	/// </summary>
	/// <param name="tableName">Name of table to open</param>
	/// <returns>A <see cref="JetCursor"/> to retrieve records from <paramref name="tableName"/>.</returns>
	/// <exception cref="ArgumentException"><paramref name="tableName"/> is <see langword="null"/> or empty.</exception>
	public JetCursor OpenTable(string tableName)
	{
		if (string.IsNullOrEmpty(tableName)) throw new ArgumentException($"'{nameof(tableName)}' cannot be null or empty.", nameof(tableName));

		this.VerifyNotDisposed();

		Api.OpenTable(this.sesid, this.dbid, tableName, OpenTableGrbit.None, out var tableId);
		return new JetCursor(this.sesid, tableId);
	}

	/// <summary>
	/// Gets a list of tables in the database.
	/// </summary>
	/// <returns>An array of <see cref="JetTableInfo"/>, one for each object in the database, including system objects.</returns>
	public JetTableInfo[] GetTables(bool includeRecordCount)
	{
		this.VerifyNotDisposed();

		Api.JetGetObjectInfo(this.sesid, this.dbid, out JET_OBJECTLIST list);
		using (JetCursor cur = new JetCursor(this.sesid, list.tableid))
		{
			List<JetTableInfo> tables = new List<JetTableInfo>(list.cRecord);
			do
			{
				string? tableName = cur.ReadUtf16String(list.columnidobjectname);
				// UNDONE: Still doesn't fetch record count
				//if (includeRecordCount)
				//{
				//	var tableInfo = this.GetTableInfo(tableName);
				//	tables.Add(tableInfo);
				//}
				//else
				{
					var table = new JetTableInfo(tableName, this,
						recordCount: 0,
						pagesUsed: 0,
						// UNDONE: Always zero from this method
						//recordCount: cur.ReadInt32(list.columnidcRecord).Value,
						//pagesUsed: cur.ReadInt32(list.columnidcPage).Value,
						flags: (ObjectInfoFlags)cur.ReadInt32(list.columnidflags)
					);
					tables.Add(table);
				}
			} while (cur.MoveNext());

			return tables.ToArray();
		}
	}

	#region Info
	public JetTableInfo GetTableInfo(string tableName)
	{
		if (string.IsNullOrEmpty(tableName)) throw new ArgumentException($"'{nameof(tableName)}' cannot be null or empty.", nameof(tableName));

		this.VerifyNotDisposed();

		Api.JetGetObjectInfo(this.sesid, this.dbid, JET_objtyp.Table, tableName, out var objInfo);
		return new JetTableInfo(tableName, this, objInfo);
	}

	public JetColumnInfo[] GetColumns(string tableName)
	{
		if (string.IsNullOrEmpty(tableName)) throw new ArgumentException($"'{nameof(tableName)}' cannot be null or empty.", nameof(tableName));

		this.VerifyNotDisposed();

		Api.JetGetColumnInfo(this.sesid, this.dbid, tableName, null, out JET_COLUMNLIST list);
		using (JetCursor cur = new JetCursor(this.sesid, list.tableid))
		{
			List<JetColumnInfo> columns = new List<JetColumnInfo>(list.cRecord);
			do
			{
				var col = new JetColumnInfo(tableName,
					cur.ReadUtf16String(list.columnidcolumnname),
					JetColumnInfo.MakeColumnId(cur.ReadInt32(list.columnidcolumnid).Value),
					(JetColumnType)cur.ReadInt32(list.columnidcoltyp).Value,
					cur.ReadInt32(list.columnidcbMax).Value,
					(ColumndefGrbit)cur.ReadInt32(list.columnidgrbit).Value
					);
				columns.Add(col);
			} while (cur.MoveNext());
			return columns.ToArray();
		}
	}

	public JET_COLUMNID GetColumnId(string tableName, string columnName)
	{
		if (string.IsNullOrEmpty(tableName)) throw new ArgumentException($"'{nameof(tableName)}' cannot be null or empty.", nameof(tableName));
		if (string.IsNullOrEmpty(columnName)) throw new ArgumentException($"'{nameof(columnName)}' cannot be null or empty.", nameof(columnName));

		this.VerifyNotDisposed();

		Api.JetGetColumnInfo(this.sesid, this.dbid, tableName, columnName, out JET_COLUMNDEF colInfo);
		return colInfo.columnid;
	}

	public JetColumnInfo GetColumnInfo(string tableName, string columnName)
	{
		if (string.IsNullOrEmpty(tableName)) throw new ArgumentException($"'{nameof(tableName)}' cannot be null or empty.", nameof(tableName));
		if (string.IsNullOrEmpty(columnName)) throw new ArgumentException($"'{nameof(columnName)}' cannot be null or empty.", nameof(columnName));

		this.VerifyNotDisposed();

		Api.JetGetColumnInfo(this.sesid, this.dbid, tableName, columnName, out JET_COLUMNDEF colInfo);
		return new JetColumnInfo(tableName, columnName, colInfo);
	}
	#endregion

	public JetIndexColumnInfo[] GetIndexColumns(string tableName)
	{
		if (string.IsNullOrEmpty(tableName)) throw new ArgumentException($"'{nameof(tableName)}' cannot be null or empty.", nameof(tableName));

		this.VerifyNotDisposed();

		Api.JetGetIndexInfo(this.sesid, this.dbid, tableName, null, out var indexList);
		using (var cur = new JetCursor(this.sesid, indexList.tableid))
		{
			List<JetIndexColumnInfo> cols = new List<JetIndexColumnInfo>(indexList.cRecord);
			do
			{
				var col = new JetIndexColumnInfo(
					indexName: cur.ReadUtf16String(indexList.columnidindexname),
					columnName: cur.ReadUtf16String(indexList.columnidcolumnname),
					flags: (JetIndexColumnGrbit)cur.ReadInt32(indexList.columnidgrbitIndex).Value,
					numberOfKeys: cur.ReadInt32(indexList.columnidcKey).Value,
					entries: cur.ReadInt32(indexList.columnidcEntry).Value,
					pages: cur.ReadInt32(indexList.columnidcPage).Value,
					columnCount: cur.ReadInt32(indexList.columnidcColumn).Value,
					columnIndex: cur.ReadInt32(indexList.columnidiColumn).Value,
					columnId: JetColumnInfo.MakeColumnId(cur.ReadInt32(indexList.columnidcolumnid).Value),
					columnType: (JetColumnType)cur.ReadInt32(indexList.columnidcoltyp).Value,
					// TODO: Obsolete
					country: 0,
					languageId: cur.ReadInt16(indexList.columnidLangid).Value,
					codePage: cur.ReadInt16(indexList.columnidCp).Value,
					// TODO: Obsolete
					collate: 0,
					columnFlags: cur.ReadInt32(indexList.columnidgrbitColumn).Value,
					lCMapFlags: cur.ReadInt32(indexList.columnidLCMapFlags).Value
					);
				cols.Add(col);
			} while (cur.MoveNext());
			return cols.ToArray();
		}
	}
}
partial class JetDatabase : IDisposable
{
	private bool disposedValue;
	protected void VerifyNotDisposed()
	{
		if (this.disposedValue)
			throw new ObjectDisposedException(Messages.JetObjectDisposedMessage);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			try
			{
				Api.JetCloseDatabase(this.sesid, this.dbid, CloseDatabaseGrbit.None);
				Api.JetDetachDatabase(this.sesid, this.FileName);
			}
			catch { }

			disposedValue = true;
		}
	}

	~JetDatabase()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: false);
	}

	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
