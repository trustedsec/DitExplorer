using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using System.ComponentModel;
using System.Windows.Media;
using DitExplorer.Ntds;
using System.Windows.Data;
using System.DirectoryServices.ActiveDirectory;
using DitExplorer.UI.WpfApp.Controls;
using DitExplorer.UI.Behaviors;

namespace DitExplorer.UI.WpfApp
{
	class DirectoryListViewModel : ViewModel, IContextCommandProvider
	{
		public DirectoryListViewModel(IItemActionRegistry actionRegistry, DirectoryView? directory)
		{
			RegisterCommand(MyCommands.ChooseColumns, ShowColumnChooser);
			RegisterCommand<object>(ApplicationCommands.Properties, InspectObject);
			this.actionRegistry = actionRegistry;
			this._directory = directory;
		}

		private ListView _itemsListView;
		private DirectoryView? _directory;
		private readonly IItemActionRegistry actionRegistry;

		protected override void OnViewLoaded(FrameworkElement viewElement)
		{
			base.OnViewLoaded(viewElement);

			var lvw = (ListView)viewElement;
			_itemsListView = lvw;
			InitListFromDirectory(lvw, _directory);
		}

		private ColumnChooserWindow? _columnChooser;

		internal void OnDirectoryLoaded(DirectoryView directory)
		{
			InitListFromDirectory(_itemsListView, directory);
		}

		private void InitListFromDirectory(ListView listView, DirectoryView? directory)
		{
			_directory = directory;
			if (directory != null && listView != null)
			{
				var nameProperty = directory.attrProps[nameof(DirectoryNode.Name)];
				var objclassProperty = directory.attrProps[nameof(DirectoryNode.ObjectClassName)];

				var grid = (GridView)listView.View;
				grid.Columns.Clear();
				grid.Columns.Add(CreateColumn(nameProperty, Messages.General_NameColumnHeader));
				grid.Columns.Add(CreateColumn(objclassProperty, Messages.Schema_ObjClassColumnHeader));
			}
		}

		internal static GridViewColumn CreateColumn(
			PropertyDescriptor property,
			string header
			)
		{
			GridViewColumn column = new GridViewColumn()
			{
				Header = header
			};
			column.SetValue(FlexGrid.DisplayPropertyProperty, property);
			ListViewSorting.SetSortProperty(column, property.Name);
			//column.DisplayMemberBinding = new Binding(property.Name);

			var isMulti = MultiValue.GetElementType(property.PropertyType) is not null;
			var binding = new Binding() { Path = new PropertyPath(property.Name) };
			DataTemplate cellTemplate =
				isMulti ? CreateMultiCellTemplate(property, binding)
				: CreateCellTemplate(property, binding);

			column.CellTemplate = cellTemplate;

			return column;
		}

		private static DataTemplate CreateCellTemplate(PropertyDescriptor? property, Binding binding)
		{
			FrameworkElementFactory cellFactory = new FrameworkElementFactory(typeof(TextBlock));
			cellFactory.SetBinding(TextBlock.TextProperty, binding);
			cellFactory.SetValue(FlexGrid.DisplayPropertyProperty, property);
			DataTemplate cellTemplate = new DataTemplate()
			{
				VisualTree = cellFactory
			};
			return cellTemplate;
		}

		private static DataTemplate CreateMultiCellTemplate(PropertyDescriptor property, Binding binding)
		{
			var contentTemplate = CreateCellTemplate(property, new Binding());

			FrameworkElementFactory cellFactory = new FrameworkElementFactory(typeof(MultiValueCell));
			cellFactory.SetBinding(ContentControl.ContentProperty, binding);
			cellFactory.SetValue(ContentControl.ContentTemplateProperty, contentTemplate);
			cellFactory.SetValue(FlexGrid.DisplayPropertyProperty, property);
			DataTemplate cellTemplate = new DataTemplate()
			{
				VisualTree = cellFactory
			};
			return cellTemplate;
		}

		public void ShowColumnChooser()
		{
			if (_itemsListView != null)
				if (_columnChooser == null)
				{
					var grid = (GridView)_itemsListView.View;
					ColumnChooserViewModel vm = new ColumnChooserViewModel(_directory.attrPropList, _directory.Directory.GetClassSchemas(), grid);
					ColumnChooserWindow wnd = new ColumnChooserWindow() { DataContext = vm, Owner = Window, WindowStartupLocation = WindowStartupLocation.CenterOwner };

					_columnChooser = wnd;
					wnd.Closed += (o, e) => { _columnChooser = null; };
					wnd.Show();
				}
				else
					_columnChooser.Activate();
		}

		private static PropertyDescriptor? GetDisplayPropertyOf(DependencyObject? target, DependencyObject owner)
		{
			while (target != null && target != owner)
			{
				var prop = FlexGrid.GetDisplayProperty(target);
				if (prop != null)
					return prop;

				target = VisualTreeHelper.GetParent(target);
			}

			return null;
		}

		class ItemActionContext : IItemActionContext
		{
			internal ItemActionContext(Window? owner)
			{
				Owner = owner;
			}

			public Window? Owner { get; }
		}

		void IContextCommandProvider.GetContextCommands(
			CommandContext context,
			FrameworkElement? target,
			DependencyObject? source)
		{
			//if (!this._itemsMenuValid)
			{
				var lvw = target as ListView;

				object? item = null;
				var cont = ItemsControl.ContainerFromElement(null, source);
				if (cont != null)
				{
					var itemsControl = ItemsControl.ItemsControlFromItemContainer(cont);
					item = itemsControl.ItemContainerGenerator.ItemFromContainer(cont);
				}

				var menu = context.Menu;

				if (context.Items.Length > 0)
				{
					var actionContext = new ItemActionContext(this.Window);
					var actions = FindItemActionsFor(context.Items, actionContext);
					foreach (var action in actions)
						menu.Items.Add(new MenuItem() { Header = action.MenuText, CommandParameter = context.Items, Command = new ItemActionCommand(action, actionContext) });
				}

				if (lvw != null)
				{
					if (menu.Items.Count > 0)
						menu.Items.Add(new Separator());

					if (context.Items.Length > 0)
					{
						var prop = GetDisplayPropertyOf(source, target);
						// CopyValue
						if (prop != null)
							menu.Items.Add(new MenuItem() { Header = Messages.Edit_CopyValueMenuText, Command = MyCommands.CopyValue, CommandParameter = new CellSpec(item, prop) });
						// CopyItems
						menu.Items.Add(new MenuItem() { Header = Messages.Edit_CopyItemsMenuText, Command = MyCommands.CopySelection, CommandParameter = lvw, CommandTarget = lvw });
						// Copy DN
						if (item is DirectoryNode node)
							menu.Items.Add(new MenuItem() { Header = Messages.Edit_CopyDnMenuText, Command = MyCommands.CopyDN, CommandParameter = context.Items });

						menu.Items.Add(new Separator());

						// Search Subtree
						menu.Items.Add(new MenuItem() { Header = Messages.Edit_SearchSubtreeMenuText, Command = MyCommands.SearchSubtree, CommandParameter = item });

						menu.Items.Add(new Separator());
					}

					// Export
					menu.Items.Add(new MenuItem() { Header = Messages.File_ExportMenuText, Command = MyCommands.ExportList });
					// Columns...
					menu.Items.Add(new MenuItem() { Header = Messages.View_ColumnsMenuText, Command = MyCommands.ChooseColumns, CommandTarget = lvw });
				}
				else if (item != null)
				{
					if (menu.Items.Count > 0)
						menu.Items.Add(new Separator());

					// Copy
					menu.Items.Add(new MenuItem() { Header = Messages.Edit_CopyMenuText, Command = ApplicationCommands.Copy, CommandParameter = item });
					// Copy DN
					if (item is DirectoryNode node)
						menu.Items.Add(new MenuItem() { Header = Messages.Edit_CopyDnMenuText, Command = MyCommands.CopyDN, CommandParameter = node });


					menu.Items.Add(new Separator());
					// Search Subtree
					menu.Items.Add(new MenuItem() { Header = Messages.Edit_SearchSubtreeMenuText, Command = MyCommands.SearchSubtree, CommandParameter = item });
				}

				if (item != null)
				{
					if (menu.Items.Count > 0)
						menu.Items.Add(new Separator());
					menu.Items.Add(new MenuItem() { Header = Messages.Object_PropertiesMenuText, Command = ApplicationCommands.Properties, CommandParameter = item });
				}
			}
		}

		private IList<ItemAction> FindItemActionsFor(
			object[] items,
			ItemActionContext actionContext
			)
		{
			return actionRegistry.GetActionsFor(items, actionContext);
		}

		private void InspectObject(object param)
		{
			Window? owner = Window;
			param = App.ShowObjectInspector(param, owner);
		}
	}
}
