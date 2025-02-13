using Microsoft.Isam.Esent.Interop;
using System.Runtime.InteropServices;
using static Microsoft.Isam.Esent.Interop.EnumeratedColumn;

namespace DitExplorer.EseInterop;

public class JetColumnInfo
{
	internal JetColumnInfo(string tableName, string columnName, JET_COLUMNDEF colInfo)
		: this(tableName, columnName,
			 colInfo.columnid,
			 (JetColumnType)colInfo.coltyp,
			 colInfo.cbMax,
			 colInfo.grbit)
	{
	}
	internal JetColumnInfo(string tableName, string columnName,
		JET_COLUMNID columnId,
		JetColumnType columnType,
		int maxSize,
		ColumndefGrbit flags)
	{
		this.TableName = tableName;
		this.ColumnName = columnName;
		this.ColumnId = columnId;
		this.ColumnType = columnType;
		this.MaxSize = maxSize;
		this.Flags = flags;
	}

	// UNDONE: Prefer MemoryMarshal
	//[StructLayout(LayoutKind.Explicit)]
	//struct ColumnIdUnion
	//{
	//	[FieldOffset(0)]
	//	internal JET_COLUMNID colid;
	//	[FieldOffset(0)]
	//	internal int value;
	//}

	/// <summary>
	/// Creates a <see cref="JET_COLUMNID"/>.
	/// </summary>
	/// <param name="value">Value of column ID</param>
	/// <returns>A <see cref="JET_COLUMNID"/> with value <paramref name="value"/></returns>
	/// <remarks>
	/// <see cref="JET_COLUMNID"/> doesn't provide a public constructor.
	/// </remarks>
	public static JET_COLUMNID MakeColumnId(int value)
		=> MemoryMarshal.Cast<int, JET_COLUMNID>(MemoryMarshal.CreateReadOnlySpan(ref value, 1))[0];
	public static int ValueOf(JET_COLUMNID id)
		=> MemoryMarshal.Cast<JET_COLUMNID, int>(MemoryMarshal.CreateReadOnlySpan(ref id, 1))[0];

	public string TableName { get; }
	public string ColumnName { get; }

	public JET_COLUMNID ColumnId { get; }
	public int ColumnIdValue => ValueOf(this.ColumnId);
	public JetColumnType ColumnType { get; }
	public int MaxSize { get; }
	public ColumndefGrbit Flags { get; }

	public bool IsFixedSize => 0 != (this.Flags & ColumndefGrbit.ColumnFixed);
	public bool IsTagged => 0 != (this.Flags & ColumndefGrbit.ColumnTagged);
	public bool IsNotNull => 0 != (this.Flags & ColumndefGrbit.ColumnNotNULL);
	public bool IsVersion => 0 != (this.Flags & ColumndefGrbit.ColumnVersion);
	public bool IsMultiValued => 0 != (this.Flags & ColumndefGrbit.ColumnMultiValued);
}