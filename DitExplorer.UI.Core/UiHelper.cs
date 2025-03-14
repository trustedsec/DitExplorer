using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DitExplorer.UI;
public static class UiHelper
{
	public static void ReportExportSuccess(string fileName, int recordCount, string title, Window? owner)
	{
		var msgres = MessageBox.Show(owner, $"Finished exporting {recordCount} records to {fileName}.\r\n\r\nWould you like to open the folder containing the file?", title, MessageBoxButton.YesNo, MessageBoxImage.Information);
		if (msgres == MessageBoxResult.Yes)
			Process.Start(new ProcessStartInfo("explorer.exe", "/select," + fileName));
	}

}
