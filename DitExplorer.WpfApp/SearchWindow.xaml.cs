using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DitExplorer.UI.WpfApp
{
	/// <summary>
	/// Interaction logic for SearchWindow.xaml
	/// </summary>
	public partial class SearchWindow : Window
	{
		public SearchWindow()
		{
			InitializeComponent();
		}

		private void searchTabs_Loaded(object sender, RoutedEventArgs e)
		{
			if (this.searchTabs.Items.Count > 0)
			{
				var item0 = this.searchTabs.Items[0];
				var tab0 = (TabItem)this.searchTabs.ItemContainerGenerator.ContainerFromItem(item0);
				var content = tab0.Content as FrameworkElement;
				if (content != null)
				{
					content.Focus();
				}
			}
		}
	}
}
