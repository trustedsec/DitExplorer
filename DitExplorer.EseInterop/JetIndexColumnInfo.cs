using Microsoft.Isam.Esent.Interop;
using Microsoft.Isam.Esent.Interop.Windows8;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.EseInterop;
public class JetIndexColumnInfo
{
	public JetIndexColumnInfo(
		string indexName,
		JetIndexColumnGrbit flags,
		int numberOfKeys,
		int entries,
		int pages,
		int columnCount,
		int columnIndex,
		JET_COLUMNID columnId,
		JetColumnType columnType,
		short country,
		short languageId,
		short codePage,
		short collate,
		int columnFlags,
		string columnName,
		int lCMapFlags)
	{
		IndexName = indexName;
		Flags = flags;
		NumberOfKeys = numberOfKeys;
		Entries = entries;
		Pages = pages;
		ColumnCount = columnCount;
		ColumnIndex = columnIndex;
		ColumnId = columnId;
		ColumnType = columnType;
		Country = country;
		LanguageId = languageId;
		CodePage = codePage;
		Collate = collate;
		ColumnFlags = columnFlags;
		ColumnName = columnName;
		LCMapFlags = lCMapFlags;
	}

	public string IndexName { get; }
	public JetIndexColumnGrbit Flags { get; }
	public int NumberOfKeys { get; }
	public int Entries { get; }
	public int Pages { get; }
	public int ColumnCount { get; }
	public int ColumnIndex { get; }
	public JET_COLUMNID ColumnId { get; }
	public JetColumnType ColumnType { get; }
	public short Country { get; }
	public short LanguageId { get; }
	public short CodePage { get; }
	public short Collate { get; }
	public int ColumnFlags { get; }
	public string ColumnName { get; }
	public int LCMapFlags { get; }
}
