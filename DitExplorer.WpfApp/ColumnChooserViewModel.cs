using DitExplorer.Ntds;
using DitExplorer.UI.Behaviors;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace DitExplorer.UI.WpfApp;
internal class ColumnChooserViewModel : ViewModel
{
	private readonly IList<PropertyDescriptor> availableProperties;
	private readonly IClassSchema[] classes;
	private readonly GridView grid;
	private readonly List<ColumnChoice> _allColumns;
	private Dictionary<string, GridViewColumn> _columnsByName;

	internal ColumnChooserViewModel(
		IList<PropertyDescriptor> availableProperties,
		IClassSchema[] classes,
		GridView grid)
	{
		RegisterCommand(MyCommands.Accept, AcceptChanges);

		this.availableProperties = availableProperties;
		this.classes = classes;
		this.grid = grid;

		// Determine which columns are visible
		Dictionary<string, GridViewColumn> columns = new Dictionary<string, GridViewColumn>();
		foreach (var col in grid.Columns)
		{
			var name = ListViewSorting.GetSortProperty(col);
			if (name != null)
				columns.Add(name, col);
		}
		_columnsByName = columns;

		List<ColumnChoice> columnChoices = new List<ColumnChoice>(availableProperties.Count);
		Dictionary<string, ColumnChoice> columnsByName = new Dictionary<string, ColumnChoice>();
		foreach (PropertyDescriptor prop in availableProperties)
		{
			if (!prop.IsBrowsable)
				continue;
			if (prop.Name == nameof(DirectoryNode.Name))
				// TODO: Find a better way to detect the Name property
				continue;

			columns.TryGetValue(prop.Name, out var col);

			var choice = new ColumnChoice(prop)
			{
				IsEnabled = prop.Name != nameof(DirectoryNode.Name)
			};
			if (col != null)
			{
				choice.originalChecked = true;
				choice.IsChecked = true;
			}

			columnChoices.Add(choice);
			if (prop is DirectoryPropertyDescriptor dirprop)
				columnsByName.Add(dirprop.AttributeSchema.LdapDisplayName, choice);
		}

		_allColumns = columnChoices;

		List<ColumnSet> columnSets = new List<ColumnSet>();
		var setAll = new ColumnSet("<All>", columnChoices);
		columnSets.Add(setAll);

		foreach (var objcls in classes)
		{
			ColumnSet set = new ColumnSet(objcls.Name, () =>
			{
				var attrs = objcls.GetAttributes(true);
				List<ColumnChoice> cols = new List<ColumnChoice>(attrs.Length);
				foreach (var attr in attrs)
					if (columnsByName.TryGetValue(attr.LdapDisplayName, out var col))
						cols.Add(col);
				return cols;
			});
			columnSets.Add(set);
		}

		ColumnSets = columnSets;
		SelectedColumnSet = setAll;
	}


	private ColumnSet _selectedColumnSet;

	public ColumnSet SelectedColumnSet
	{
		get { return _selectedColumnSet; }
		set
		{
			if (NotifyIfChanged(ref _selectedColumnSet, value))
				UpdateColumns(value, ColumnSearch);
		}
	}

	public List<ColumnChoice> Columns { get; private set; }

	private void AcceptChanges()
	{
		foreach (var choice in _allColumns)
			if (choice.IsChecked != choice.originalChecked)
				if (choice.IsChecked)
				{
					var col = DirectoryListViewModel.CreateColumn(choice.Property, choice.Property.DisplayName);
					grid.Columns.Add(col);
				}
				else
					if (_columnsByName.TryGetValue(choice.Name, out var col))
					grid.Columns.Remove(col);

		ICommand cmd = ApplicationCommands.Close;
		cmd.Execute(null);
	}

	private string? _columnSearch;

	public string? ColumnSearch
	{
		get { return _columnSearch; }
		set
		{
			if (NotifyIfChanged(ref _columnSearch, value))
				_ = Task.Factory.StartNew(() => UpdateColumns(_selectedColumnSet, value));
		}
	}

	public IList<ColumnSet> ColumnSets { get; }

	private void UpdateColumns(ColumnSet? set, string? searchText)
	{
		var columns = set?.Columns ?? new List<ColumnChoice>();
		if (searchText != null)
			columns = columns.FindAll(r =>
				r.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
				|| (r.Property.DisplayName?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
				|| (r.Property.Description?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
				);

		if (ColumnSearch == searchText && SelectedColumnSet == set)
		{
			Columns = columns;
			OnPropertyChanged(nameof(Columns));
		}
	}
}

class ColumnSet
{
	private List<ColumnChoice>? _columns;
	private readonly Func<List<ColumnChoice>> _columnsFactory;

	internal List<ColumnChoice> Columns => _columns ??= _columnsFactory();

	public string Name { get; }

	internal ColumnSet(string name, List<ColumnChoice> columns)
	{
		Name = name;
		_columns = columns;
	}
	internal ColumnSet(string name, Func<List<ColumnChoice>> columnsFactory)
	{
		Name = name;
		_columnsFactory = columnsFactory;
	}

	public sealed override string ToString()
		=> Name;
}

class ColumnChoice : Notifier
{
	internal ColumnChoice(PropertyDescriptor property)
	{
		Property = property;
	}
	public bool IsEnabled { get; set; }
	public string? Description => Property.Description;
	public PropertyDescriptor Property { get; }
	public string Name => Property.Name;

	private bool _isVisible;

	internal bool originalChecked;
	public bool IsChecked
	{
		get { return _isVisible; }
		set => NotifyIfChanged(ref _isVisible, value);
	}

}