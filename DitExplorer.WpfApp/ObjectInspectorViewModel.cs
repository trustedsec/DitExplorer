using DitExplorer.Ntds;
using DitExplorer.UI.WpfApp;
using DitExplorer.UI.WpfApp.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DitExplorer.UI.WpfApp;
internal partial class ObjectInspectorViewModel : ViewModel
{
	public IDirectoryObject Object { get; }

	internal ObjectInspectorViewModel(DirectoryNode node, IItemActionRegistry itemActionRegistry)
	{
		IDirectoryObject obj = node.Object;
		this.Object = obj;
		this._dirView = node.Owner;
		this.Title = Messages.ObjectInspector_Title + " - " + obj.DistinguishedName;

		var dir = obj.Directory;
		var allAttrs = dir.GetAllAttributeSchemas();

		object[] allValues = obj.GetValueOfMultiple(allAttrs);
		List<PropertyItem> propertyItems = new List<PropertyItem>();
		for (int i = 0; i < allAttrs.Length; i++)
		{
			var attr = allAttrs[i];
			var value = allValues[i];

			var ntdsAttr = attr as NtdsAttributeSchema;
			if (value is MultiValue multi)
			{
				for (int j = 0; j < multi.Count; j++)
				{
					var element = multi.GetElementAt(j);
					var item = new PropertyItem(attr.Name, j + 1, element) { LdapName = attr.LdapDisplayName, ColumnName = ntdsAttr?.ColumnName };
					propertyItems.Add(item);
				}
			}
			else if (value != null)
			{
				var item = new PropertyItem(attr.Name, 1, value) { LdapName = attr.LdapDisplayName, ColumnName = ntdsAttr?.ColumnName };
				propertyItems.Add(item);
			}
		}

		this._allProps = propertyItems;
		this.Properties = propertyItems;

		this.HasMembers = obj.ObjectClass.HasAttribute("member");
		if (this.HasMembers)
		{
			this.MembersListVM = new DirectoryListViewModel(itemActionRegistry, this._dirView);
		}
		this.HasMemberOf = obj.ObjectClass.HasAttribute("memberOf");
		if (this.HasMemberOf)
		{
			this.MemberOfListVM = new DirectoryListViewModel(itemActionRegistry, this._dirView);
		}
	}

	public string Title { get; }

	private readonly List<PropertyItem> _allProps;
	private readonly DirectoryView _dirView;
	private List<PropertyItem> _props;

	public List<PropertyItem> Properties
	{
		get { return _props; }
		set => this.NotifyIfChanged(ref _props, value);
	}

	public bool HasMembers { get; }
	public bool HasMemberOf { get; }

	private List<IDirectoryNode>? _members;
	public List<IDirectoryNode> Members => (this._members ??= this.GetMembers());
	private List<IDirectoryNode>? GetMembers()
	{
		return this.Object.GetMembers().Select(r => (IDirectoryNode)new DirectoryNode(r, this._dirView)).ToList();
	}
	public DirectoryListViewModel? MembersListVM { get; }


	private List<IDirectoryNode>? _memberOf;
	public List<IDirectoryNode> MemberOf => (this._memberOf ??= this.GetMemberOf());
	private List<IDirectoryNode>? GetMemberOf()
	{
		return this.Object.GetMemberOfGroups().Select(r => (IDirectoryNode)new DirectoryNode(r, this._dirView)).ToList();
	}
	public DirectoryListViewModel? MemberOfListVM { get; }

	internal record struct NameSortKey(string name, int seq) : IComparable<NameSortKey>
	{
		public int CompareTo(NameSortKey other)
		{
			int cmp = StringComparer.OrdinalIgnoreCase.Compare(this.name, other.name);
			if (cmp != 0)
				return cmp;

			return this.seq - other.seq;
		}
	}

	internal class PropertyItem
	{
		public PropertyItem(string name, int seq, object? value)
		{
			Name = name;
			Seq = seq;
			Value = value;
		}

		public string Name { get; }
		internal NameSortKey NameSortKey => new NameSortKey(this.Name, this.Seq);
		public int Seq { get; }
		public string LdapName { get; set; }
		public string? ColumnName { get; set; }
		public object? RawValue { get; set; }
		public object? Value { get; set; }

		/// <summary>
		/// Provides a value for sorting.
		/// </summary>
		/// <remarks>
		/// The default WPF sort behavior will fail to compare objects of different types.
		/// Although basic, this property provides a uniform string array for comparison.
		/// It won't handle numbers well.
		/// </remarks>
		// TODO: More intelligent sorting
		public string? SortValue => this.Value?.ToString();
	}

	private string? _propertySearch;

	public string? PropertySearch
	{
		get { return _propertySearch; }
		set
		{
			if (this.NotifyIfChanged(ref _propertySearch, value))
				this.UpdateProperties(value);
		}
	}

	private void UpdateProperties(string? value)
	{
		var props = this._allProps;
		if (value != null)
		{
			props = props.FindAll(r =>
				r.Name.Contains(value, StringComparison.OrdinalIgnoreCase)
				|| r.LdapName.Contains(value, StringComparison.OrdinalIgnoreCase)
				|| (r.RawValue?.ToString()?.Contains(value, StringComparison.OrdinalIgnoreCase) ?? false)
				|| (r.SortValue?.Contains(value, StringComparison.OrdinalIgnoreCase) ?? false)
				|| (r.ColumnName?.Contains(value, StringComparison.OrdinalIgnoreCase) ?? false)
				);
		}

		if (this.PropertySearch == value)
		{
			this.Properties = props;
		}
	}
}

internal partial class ObjectInspectorViewModel : IContextCommandProvider
{
	void IContextCommandProvider.GetContextCommands(CommandContext context, FrameworkElement? target, DependencyObject? source)
	{
		var menu = context.Menu;
		var lvw = target as ListView;
		if (context.Items.Length > 0)
		{
			// CopyItems
			menu.Items.Add(new MenuItem() { Header = Messages.Edit_CopyItemsMenuText, Command = MyCommands.CopySelection, CommandParameter = lvw, CommandTarget = lvw });
		}
	}
}
