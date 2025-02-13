using DitExplorer.EseInterop;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DitExplorer.UI.WpfApp
{
	class DatabaseSchemaViewModel : ViewModel, IContextCommandProvider
	{
		internal DatabaseSchemaViewModel(JetDatabase db, JetSession? session, bool ownsDb)
		{
			this.db = db;
			this.session = session;
			this.ownsDb = ownsDb;
			Tables = db.GetTables(true);

			Title = Messages.DatabaseSchemaViewer_Title + " - " + db.FileName;

			RegisterCommand(MyCommands.ExportTableData, ExportTableData, CanExportTableData);
		}

		public string Title { get; }

		protected override void OnViewUnloaded()
		{
			if (ownsDb)
			{
				db.Dispose();
				session?.Dispose();
			}
		}

		private bool CanExportTableData()
			=> SelectedTable != null;

		private void ExportTableData()
		{
			var table = SelectedTable;
			if (table != null)
			{
				SaveFileDialog dl = new SaveFileDialog()
				{
					Title = Messages.File_ExportTableDataTitle,
					Filter = "Tab-delimited text (*.txt)|*.txt|Comma-separated values (*.csv)|*.csv",
					FileName = table.TableName
				};

				var res = dl.ShowDialog(Window);
				if (res ?? false)
				{
					string fileName = dl.FileName;
					try
					{
						var columns = db.GetColumns(table.TableName);

						int recordCount = 0;
						using (StreamWriter writer = File.CreateText(fileName))
						{
							TabularTextWriter builder = dl.FilterIndex switch
							{
								1 => new TsvBuilder(writer),
								_ => new CsvBuilder(writer)
							};

							using (var cursor = db.OpenTable(table.TableName))
							{
								foreach (var col in columns)
									builder.WriteValue(col.ColumnName);
								builder.EndRecord();

								do
								{
									foreach (var col in columns)
									{
										int tag = 1;
										object value = col.ColumnType switch
										{
											JetColumnType.Bit => cursor.ReadBit(col.ColumnId, tag),
											JetColumnType.UnsignedByte => cursor.ReadByte(col.ColumnId, tag),
											JetColumnType.Short => cursor.ReadInt16(col.ColumnId, tag),
											JetColumnType.Long => cursor.ReadInt32(col.ColumnId, tag),
											JetColumnType.Currency or JetColumnType.LongLong => cursor.ReadInt64(col.ColumnId, tag),
											JetColumnType.Single => cursor.ReadSingle(col.ColumnId, tag),
											JetColumnType.Double => cursor.ReadDouble(col.ColumnId, tag),
											JetColumnType.DateTime => cursor.ReadDateTime(col.ColumnId, tag),
											JetColumnType.Binary => cursor.ReadBytes(col.ColumnId, tag),
											JetColumnType.Text => cursor.ReadUtf16String(col.ColumnId, tag),
											JetColumnType.LongBinary => cursor.ReadBytes(col.ColumnId, tag),
											JetColumnType.LongText => cursor.ReadUtf16String(col.ColumnId, tag),
											//JetColumnType.SLV => throw new NotImplementedException(),
											JetColumnType.UnsignedLong => cursor.ReadUInt32(col.ColumnId, tag),
											JetColumnType.Guid => cursor.ReadGuid(col.ColumnId, tag),
											JetColumnType.UnsignedShort => cursor.ReadUInt16(col.ColumnId, tag),
											_ => null
										};

										if (value is byte[] bytes)
											value = bytes.ToHexString();
										builder.WriteValue(value?.ToString());
									}

									builder.EndRecord();
									recordCount++;
								} while (cursor.MoveNext());
							}
						}

						var msgres = MessageBox.Show(Window, $"Finished exporting {recordCount} records to {fileName}.\r\n\r\nWould you like to open the folder containing the file?", Messages.File_ExportTableDataTitle, MessageBoxButton.YesNo, MessageBoxImage.Information);
						if (msgres == MessageBoxResult.Yes)
							Process.Start(new ProcessStartInfo("explorer.exe", "/select," + fileName));
					}
					catch (Exception ex)
					{
						ReportError("Unable to export records to a file: " + ex.Message, Messages.File_ExportTableDataTitle, ex);
					}
				}
			}
		}

		private readonly JetDatabase db;
		private readonly JetSession? session;
		private readonly bool ownsDb;

		public JetTableInfo[] Tables { get; }

		private JetTableInfo? _selectedTable;

		public JetTableInfo? SelectedTable
		{
			get { return _selectedTable; }
			set
			{
				if (NotifyIfChanged(ref _selectedTable, value))
				{
					var tableName = value?.TableName;
					_allTableColumns = tableName is null ? null : db.GetColumns(tableName);
					_allTableIndexes = tableName is null ? null : db.GetIndexColumns(tableName);
					_ = Task.Factory.StartNew(() => UpdateColumns(value, ColumnSearchText));
					_ = Task.Factory.StartNew(() => UpdateIndexes(value, IndexSearchText));

					//CommandManager.InvalidateRequerySuggested();
				}
			}
		}

		private string? _columnSearchText;

		public string? ColumnSearchText
		{
			get { return _columnSearchText; }
			set
			{
				if (NotifyIfChanged(ref _columnSearchText, value))
					_ = Task.Factory.StartNew(() => UpdateColumns(SelectedTable, value));
			}
		}


		private string? _indexSearchText;

		public string? IndexSearchText
		{
			get { return _indexSearchText; }
			set
			{
				if (NotifyIfChanged(ref _indexSearchText, value))
					_ = Task.Factory.StartNew(() => UpdateIndexes(SelectedTable, value));
			}
		}

		private void UpdateColumns(JetTableInfo? table, string? searchText)
		{
			JetColumnInfo[] columns = _allTableColumns;
			if (columns != null && !string.IsNullOrEmpty(searchText))
				columns = Array.FindAll(columns, r => r.ColumnName.Contains(searchText, StringComparison.OrdinalIgnoreCase));

			if (SelectedTable == table && ColumnSearchText == searchText)
				TableColumns = columns;
		}

		private void UpdateIndexes(JetTableInfo? table, string? searchText)
		{
			var indexes = _allTableIndexes;
			if (indexes != null && !string.IsNullOrEmpty(searchText))
				indexes = Array.FindAll(indexes, r =>
					r.ColumnName.Contains(searchText, StringComparison.OrdinalIgnoreCase)
					|| r.IndexName.Contains(searchText, StringComparison.OrdinalIgnoreCase)
				);

			if (SelectedTable == table && IndexSearchText == searchText)
				IndexColumns = indexes;
		}

		private JetColumnInfo[]? _allTableColumns;
		private JetColumnInfo[]? _tableColumns;
		public JetColumnInfo[]? TableColumns
		{
			get { return _tableColumns; }
			set => NotifyIfChanged(ref _tableColumns, value);
		}

		private JetIndexColumnInfo[]? _allTableIndexes;
		private JetIndexColumnInfo[]? _indexColumns;
		public JetIndexColumnInfo[]? IndexColumns
		{
			get { return _indexColumns; }
			set => NotifyIfChanged(ref _indexColumns, value);
		}



		void IContextCommandProvider.GetContextCommands(CommandContext context, FrameworkElement? target, DependencyObject? source)
		{
			var menu = context.Menu;
			var lvw = target as ListView;
			if (context.Items.Length > 0)
				// CopyItems
				menu.Items.Add(new MenuItem() { Header = Messages.Edit_CopyItemsMenuText, Command = MyCommands.CopySelection, CommandParameter = lvw, CommandTarget = lvw });
		}
	}
}
