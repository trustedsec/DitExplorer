using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DitExplorer.UI.WpfApp;
internal class MyCommands
{
	private static InputGestureCollection MakeGesture(Key key, ModifierKeys mod = ModifierKeys.None)
	{
		return new InputGestureCollection()
		{
			new KeyGesture(key, mod)
		};
	}
	public static RoutedUICommand ChooseColumns = new RoutedUICommand(Messages.View_ColumnsMenuText, "View.SelectColumns", typeof(MyCommands), MakeGesture(Key.F7));
	public static RoutedUICommand ExportList = new RoutedUICommand(Messages.File_ExportMenuText, "List.Export", typeof(MyCommands), MakeGesture(Key.X, ModifierKeys.Alt | ModifierKeys.Control));
	public static RoutedUICommand CopyValue = new RoutedUICommand(Messages.Edit_CopyValueMenuText, "List.CopyValue", typeof(MyCommands), null);
	public static RoutedUICommand CopySelection = new RoutedUICommand(Messages.Edit_CopyMenuText, "List.CopyRows", typeof(MyCommands), MakeGesture(Key.C, ModifierKeys.Control));
	public static RoutedUICommand CopyDN = new RoutedUICommand(Messages.Edit_CopyDnMenuText, "Edit.CopyDN", typeof(MyCommands));
	public static RoutedUICommand SearchSubtree = new RoutedUICommand(Messages.Edit_SearchSubtreeMenuText, "Edit.SearchSubtree", typeof(MyCommands), MakeGesture(Key.F, ModifierKeys.Control));
	public static RoutedUICommand SearchNow = new RoutedUICommand(Messages.Edit_SearchNowAccessText, "Edit.SearchNow", typeof(MyCommands));

	public static RoutedUICommand ViewDatabaseSchema = new RoutedUICommand(Messages.View_DatabaseSchema, "View.DatabaseSchema", typeof(MyCommands), MakeGesture(Key.F8, ModifierKeys.None));

	public static RoutedUICommand ExportTableData = new RoutedUICommand(Messages.File_ExportTableDataMenuText, "DataViewer.ExportTableData", typeof(MyCommands), MakeGesture(Key.E, ModifierKeys.Control));

	public static RoutedCommand Accept = new RoutedCommand();
	public static RoutedCommand Cancel = new RoutedCommand();

	public static RoutedCommand ShowAllValues = new RoutedCommand("View.ShowAllValues", typeof(MyCommands));
}
