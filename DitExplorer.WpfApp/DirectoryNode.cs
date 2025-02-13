using DitExplorer.Ntds;
using DitExplorer.UI.WpfApp;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace DitExplorer.UI.WpfApp;

internal partial class DirectoryNode : Notifier
{
	[Browsable(false)]
	public IDirectoryObject Object { get; }

	internal DirectoryNode(IDirectoryObject obj, DirectoryView owner)
	{
		this.Object = obj;
		this.Owner = owner;
	}

	internal DirectoryView Owner { get; }

	public override string ToString() => this.Object.Name;
	public string Name => this.Object.Name;

	private bool _isExpanded;

	[Browsable(false)]
	public bool IsExpanded
	{
		get { return _isExpanded; }
		set => this.NotifyIfChanged(ref this._isExpanded, value);
	}


	[DisplayName("Obj. Class")]
	public string ObjectClassName => this.Object.ObjectClass.LdapDisplayName;

	[DisplayName("Path")]
	public string ObjectPath => this.Object.ObjectPath;

	[DisplayName("DN")]
	public string DistinguishedName => this.Object.DistinguishedName;


	private ObservableCollection<DirectoryNode>? _children;
	private CollectionView? _childrenView;

	[Browsable(false)]
	public object? ChildNodesView
	{
		get
		{
			if (this._childrenView == null)
			{
				this._children = new ObservableCollection<DirectoryNode>();
				this._childrenView = new ListCollectionView(this._children);

				// TODO: Run in background once Jet session synchronization is implemented
				this.PopulateChildren();
			}
			return this._childrenView;
		}
	}

	private async void PopulateChildren()
	{
		//try
		{
			var children = this.Object.GetChildren().Select(r => this.Owner.NodeForObject(r));

			var items = this._children;
			foreach (var item in children)
			{
				await Task.Yield();
				items.Add(item);
			}
		}
		//catch
		//{
		//	// TODO: Display error
		//}
	}
}

partial class DirectoryNode : ICustomTypeDescriptor
{
	private static EventDescriptorCollection emptyEvents = new EventDescriptorCollection(Array.Empty<EventDescriptor>(), true);
	private static AttributeCollection emptyAttrs = new AttributeCollection(Array.Empty<Attribute>());

	AttributeCollection ICustomTypeDescriptor.GetAttributes() => emptyAttrs;
	string? ICustomTypeDescriptor.GetClassName() => this.GetType().Name;
	string? ICustomTypeDescriptor.GetComponentName() => null;
	TypeConverter ICustomTypeDescriptor.GetConverter() => null;
	EventDescriptor? ICustomTypeDescriptor.GetDefaultEvent() => null;
	PropertyDescriptor? ICustomTypeDescriptor.GetDefaultProperty() => null;
	object? ICustomTypeDescriptor.GetEditor(Type editorBaseType) => null;

	EventDescriptorCollection ICustomTypeDescriptor.GetEvents() => emptyEvents;

	EventDescriptorCollection ICustomTypeDescriptor.GetEvents(Attribute[]? attributes) => emptyEvents;
	PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties() => this.Owner.attrProps;

	PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[]? attributes)
	{
		if (attributes is null || attributes.Length == 0)
			return this.Owner.attrProps;

		var allProps = this.Owner.attrProps;
		List<PropertyDescriptor> matchingProps = new List<PropertyDescriptor>();
		foreach (PropertyDescriptor prop in allProps)
		{
			bool allFiltersMatch = MatchesFilters(prop, attributes);

			if (allFiltersMatch)
				matchingProps.Add(prop);
		}

		return new PropertyDescriptorCollection(matchingProps.ToArray(), true);
	}

	private static bool MatchesFilters(PropertyDescriptor prop, Attribute[]? attributes)
	{
		var propAttrs = prop.Attributes;

		bool allFiltersMatch = true;
		foreach (Attribute filterAttr in attributes)
		{
			if (filterAttr is not null)
			{
				bool foundMatch = false;
				foreach (Attribute attr in propAttrs)
				{
					if (attr is not null)
					{
						foundMatch = attr.Match(filterAttr);
						if (foundMatch)
							break;
					}
				}

				allFiltersMatch &= foundMatch;
			}

			if (!allFiltersMatch)
				break;
		}

		return allFiltersMatch;
	}

	object? ICustomTypeDescriptor.GetPropertyOwner(PropertyDescriptor? pd) => this;
}

internal partial class DirectoryNode : Notifier, IDirectoryNode
{
	IDirectoryView IDirectoryNode.DirectoryView => this.Owner;
}

