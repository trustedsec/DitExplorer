using DitExplorer.Ntds;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using static Microsoft.Isam.Esent.Interop.EnumeratedColumn;

namespace DitExplorer.UI.WpfApp.Search
{
	[Export(typeof(IDirectorySearchProvider))]
	internal class AttributeSearchProvider : IDirectorySearchProvider
	{
		public IDirectorySearchHandler GetSearchHandler(IDirectoryObject searchRoot, ViewTechnology viewTech)
			=> viewTech switch
			{
				ViewTechnology.Wpf => new AttributeSearchHandler(searchRoot)
			};
	}

	internal class AttributeSearchHandler : ViewModel, IDirectorySearchHandler
	{
		private IDirectoryObject searchRoot;
		private IAttributeSchema[] _searchAttrs;

		public AttributeSearchHandler(IDirectoryObject searchRoot)
		{
			this.searchRoot = searchRoot;

			var dir = this.searchRoot.Directory;
			var attrs = dir.GetAllAttributeSchemas();
			Array.Sort(attrs, (x, y) => x.LdapDisplayName.CompareTo(y.LdapDisplayName));
			this._searchAttrs = attrs;

			this.RegisterCommand<SearchCondition>(ApplicationCommands.Delete, DeleteCondition);

			this.AddNewCondition();
		}

		public string Caption => Messages.Search_ByAttributeCaption;

		public IEnumerable<IDirectoryObject> GetResults()
		{
			List<DirectoryQueryClause> clauses = new List<DirectoryQueryClause>();
			foreach (var cond in this.Conditions)
			{
				if (cond != null && cond.Attribute != null)
					clauses.Add(new AttributeClause(cond.Attribute, cond.SelectedOperator.Item, cond.Comparand));
			}
			DirectoryQuery query = new DirectoryQuery(new ConjunctionClause(QueryConjunctionType.All, clauses.ToArray()));

			var results = this.searchRoot.SearchSubtree(query);
			return results;
		}

		public object? GetView()
		{
			return new AttributeSearchPage() { DataContext = this };
		}

		internal class SearchCondition : Notifier
		{
			internal SearchCondition(AttributeSearchHandler owner)
			{
				this.owner = owner;
			}

			private readonly AttributeSearchHandler owner;

			private IAttributeSchema? _attribute;
			public IAttributeSchema? Attribute
			{
				get { return _attribute; }
				set
				{
					if (this.NotifyIfChanged(ref this._attribute, value))
					{
						var attr = value;
						List<TypedListItem<ComparisonOperatorType>> ops = new List<TypedListItem<ComparisonOperatorType>>();
						if (attr != null)
						{
							ops.Add(new TypedListItem<ComparisonOperatorType>(ComparisonOperatorType.IsPresent, "is present"));
							ops.Add(new TypedListItem<ComparisonOperatorType>(ComparisonOperatorType.IsNotPresent, "is not present"));
							if (attr.Syntax.SupportsParse)
							{
								ops.Add(new TypedListItem<ComparisonOperatorType>(ComparisonOperatorType.EqualTo, "="));
								ops.Add(new TypedListItem<ComparisonOperatorType>(ComparisonOperatorType.NotEqualTo, "!="));
								if (attr.Syntax.SupportsComparison)
								{
									ops.Add(new TypedListItem<ComparisonOperatorType>(ComparisonOperatorType.LessThan, "<"));
									ops.Add(new TypedListItem<ComparisonOperatorType>(ComparisonOperatorType.LessThanOrEqualTo, "<="));
									ops.Add(new TypedListItem<ComparisonOperatorType>(ComparisonOperatorType.GreaterThan, ">"));
									ops.Add(new TypedListItem<ComparisonOperatorType>(ComparisonOperatorType.GreaterThanOrEqualTo, ">="));
								}
								if (attr.Syntax.SupportsFlags)
								{
									ops.Add(new TypedListItem<ComparisonOperatorType>(ComparisonOperatorType.HasFlag, "has flag"));
									ops.Add(new TypedListItem<ComparisonOperatorType>(ComparisonOperatorType.DoesntHaveFlag, "doesn't have flag"));
									ops.Add(new TypedListItem<ComparisonOperatorType>(ComparisonOperatorType.HasAllFlags, "has all flags"));
								}
							}

							var defOp = ops.FirstOrDefault(r => r.Item == this._selectedOp) ?? ops[0];
							this.SelectedOperator = defOp;
						}
						this.OperatorList = ops;
						this.owner.NotifyConditionChanged(this);
					}
				}
			}

			public IList<IAttributeSchema> AttributeList => this.owner._searchAttrs;

			private IList<TypedListItem<ComparisonOperatorType>> _ops;
			public IList<TypedListItem<ComparisonOperatorType>> OperatorList
			{
				get { return _ops; }
				set => this.NotifyIfChanged(ref _ops, value);
			}

			private ComparisonOperatorType _selectedOp;
			private TypedListItem<ComparisonOperatorType> _selectedOpItem;
			public TypedListItem<ComparisonOperatorType> SelectedOperator
			{
				get { return _selectedOpItem; }
				set
				{
					if (this.NotifyIfChanged(ref _selectedOpItem, value))
					{
						if (value != null)
						{
							this._selectedOp = value.Item;
							bool requiresValue = value.Item switch
							{
								ComparisonOperatorType.IsPresent or ComparisonOperatorType.IsNotPresent => false,
								_ => true
							};
							this.RequiresValueEntry = requiresValue;
						}
						else
							this._selectedOp = ComparisonOperatorType.EqualTo;
					}
				}
			}

			private bool _requiresValueEntry;

			public bool RequiresValueEntry
			{
				get { return _requiresValueEntry; }
				set => this.NotifyIfChanged(ref _requiresValueEntry, value);
			}


			private object? _parsed;
			private bool _hasParsedValue = true;
			public object? Comparand => this._parsed;

			private string? _criterion;
			public string? Criterion
			{
				get { return _criterion; }
				set
				{
					if (this.NotifyIfChanged(ref _criterion, value))
					{
						try
						{
							this._parsed = this.Attribute.Syntax.ParseValue(value);
							this._hasParsedValue = true;
						}
						catch
						{
							this._hasParsedValue = false;
							throw;
						}
					}
				}
			}

			private ObservableCollection<string> _suggestions;
			public ObservableCollection<string> Suggestions
			{
				get { return _suggestions; }
				set => this.NotifyIfChanged(ref _suggestions, value);
			}


			private bool _canDelete;

			public bool CanDelete
			{
				get { return _canDelete; }
				set => this.NotifyIfChanged(ref _canDelete, value);
			}
		}


		internal sealed class TypedListItem<TItem>
		{
			public TypedListItem(TItem item, string displayText)
			{
				Item = item;
				DisplayText = displayText;
			}

			public sealed override string ToString()
				=> this.DisplayText;

			public TItem Item { get; }
			public string DisplayText { get; }
		}

		public ObservableCollection<SearchCondition> Conditions { get; } = new ObservableCollection<SearchCondition>();

		/// <summary>
		/// Tracks the condition at the end of the list
		/// </summary>
		private SearchCondition _newCondition;

		private void NotifyConditionChanged(SearchCondition condition)
		{
			if (condition == this._newCondition)
			{
				condition.CanDelete = true;
				AddNewCondition();
			}
			if (condition.Attribute != null)
			{
				var type = condition.Attribute.Syntax?.AttributeType;

				ObservableCollection<string> suggList = new ObservableCollection<string>();
				if (type == typeof(int) || type == typeof(long))
				{
					suggList.Add("0");
					suggList.Add("1");
				}
				else if (type == typeof(bool))
				{
					suggList.Add("true");
					suggList.Add("false");
				}
				else if (type == typeof(DirectoryTime))
				{
					suggList.Add("@Today");
					suggList.Add("@Today - 30d");
					suggList.Add("@Now");
					suggList.Add("@Now - 8h");
				}
				else if (type.IsEnum)
				{
					var names = Enum.GetNames(type);
					foreach (var name in names)
					{
						suggList.Add(name);
					}
				}

				condition.Suggestions = suggList;
			}
		}

		private void AddNewCondition()
		{
			this._newCondition = new SearchCondition(this);
			this.Conditions.Add(this._newCondition);
		}

		private void DeleteCondition(SearchCondition condition)
		{
			if (condition != this._newCondition)
				this.Conditions.Remove(condition);
		}
	}
}
