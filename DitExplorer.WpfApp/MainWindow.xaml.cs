using DitExplorer.UI.WpfApp;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DitExplorer.UI.WpfApp;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
	}

	private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
	{
		TreeView tvw = (TreeView)sender;
		var vm = (AppViewModel)this.DataContext;
		vm.OnTreeNodeSelected(e.NewValue);
	}

	private void itemsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		ListView lvw = (ListView)sender;
		var vm = (AppViewModel)this.DataContext;
		vm.OnSelectionChanged();
	}
}