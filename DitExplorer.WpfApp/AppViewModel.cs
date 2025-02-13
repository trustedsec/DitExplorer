using DitExplorer.EseInterop;
using DitExplorer.Ntds;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;

namespace DitExplorer.UI.WpfApp;

internal partial class AppViewModel : ViewModel
{
	internal AppViewModel()
	{
		// TODO: Restrict some of these commands based on state, such as whether a directory is open

		var itemActionRegistry = (IItemActionRegistry)App.services.GetService(typeof(IItemActionRegistry));
		ListVM = new DirectoryListViewModel(itemActionRegistry, null);

		RegisterCommand(ApplicationCommands.Open, OpenDirectoryFile);
		//RegisterCommand(MyCommands.ChooseColumns, () => this.ListVM?.ShowColumnChooser(), );
		RegisterCommand<object>(ApplicationCommands.Properties, InspectObject);
		RegisterCommand(MyCommands.ViewDatabaseSchema, ViewDatabaseSchema, CanViewDatabaseSchema);
	}

	internal IDirectory? CurrentDirectory { get; private set; }
	public bool HasOpenDirectory => (this.CurrentDirectory != null);

	public DirectoryListViewModel ListVM { get; }


	private string? _windowTitle = Messages.MainWindowTitle;
	public string? WindowTitle
	{
		get { return _windowTitle; }
		set => NotifyIfChanged(ref _windowTitle, value);
	}

	protected override void OnViewUnloaded()
	{
		CurrentDirectory?.Dispose();
	}

	public void OpenDirectoryFile()
	{
		OpenFileDialog of = new OpenFileDialog()
		{
			Title = Messages.File_OpenDitFileTitle,
			Filter = "DIT files (*.dit)|*.dit|All files (*.*)|*.*",
		};

		var res = of.ShowDialog(Window);
		if (res ?? false)
		{
			string fileName = of.FileName;
			this.OpenDirectoryFile(fileName);
		}
	}
	public void OpenDirectoryFile(string? fileName)
	{
		if (fileName != null)
			try
			{
				var dir = NtdsDirectory.Open(fileName, null, DirectoryOpenOptions.ReadOnly);
				CurrentDirectory = dir;
				this.OnPropertyChanged(nameof(HasOpenDirectory));
				WindowTitle = Messages.MainWindowTitle + " - " + fileName;

				DirectoryView dirView = new DirectoryView(dir);
				_dirView = dirView;

				DirectoryNode node = dirView.NodeForObject(CurrentDirectory.RootDomain);
				node.IsExpanded = true;

				RootNodes.Add(node);
				SelectedNode = node;

				ListVM.OnDirectoryLoaded(dirView);
			}
			catch (Exception ex)
			{
				JetSession? ses = null;
				try
				{
					ses = JetSession.Begin();
					JetDatabase? db = null;
					try
					{
						db = ses.AttachAndOpen(fileName, OpenDatabaseOptions.ReadOnly);
						ViewDatabaseSchema(db, ses);
						db = null;
						ses = null;
					}
					finally
					{
						db?.Dispose();
					}
				}
				finally
				{
					ses?.Dispose();
				}
				ReportError(Messages.App_DitOpenFailed, Messages.File_OpenDitFileTitle, ex);
			}

		CommandManager.InvalidateRequerySuggested();
	}

	public ObservableCollection<DirectoryNode> RootNodes { get; } = new ObservableCollection<DirectoryNode>();

	private DirectoryNode? _selectedNode;

	public DirectoryNode? SelectedNode
	{
		get { return _selectedNode; }
		private set
		{
			if (NotifyIfChanged(ref _selectedNode, value))
				StartPopulateItems(value);
		}
	}

	internal void OnTreeNodeSelected(object newValue)
	{
		var dirobj = newValue as DirectoryNode;
		if (dirobj != null)
			SelectedNode = dirobj;
	}

	private ObservableCollection<DirectoryNode>? _items;
	private DirectoryView _dirView;

	public ObservableCollection<DirectoryNode>? Items
	{
		get { return _items; }
		set => NotifyIfChanged(ref _items, value);
	}

	private void StartPopulateItems(DirectoryNode? node)
	{
		if (node != null)
		{
			var disp = Dispatcher.CurrentDispatcher;

			ObservableCollection<DirectoryNode> items = new ObservableCollection<DirectoryNode>();
			foreach (var obj in node.Object.GetChildren())
				items.Add(_dirView.NodeForObject(obj));
			Items = items;
		}
	}

	internal void OnSelectionChanged()
	{
	}


	private void InspectObject(object param)
	{
		Window? owner = Window;
		param = App.ShowObjectInspector(param, owner);
	}

	private bool CanViewDatabaseSchema()
		=> CurrentDirectory is NtdsDirectory;

	private void ViewDatabaseSchema()
		=> ViewDatabaseSchema(((NtdsDirectory)CurrentDirectory).Database, null);
	private void ViewDatabaseSchema(JetDatabase db, JetSession? session)
	{
		DatabaseSchemaViewModel vm = new DatabaseSchemaViewModel(db, session, false);
		DatabaseSchemaWindow wnd = new DatabaseSchemaWindow() { DataContext = vm, Owner = Window, WindowStartupLocation = WindowStartupLocation.CenterOwner };
		wnd.Show();
	}
}

