using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace DitExplorer.UI.Behaviors;

/// <summary>
/// Implements list sort behavior for a <see cref="ListView"/>.
/// </summary>
/// <remarks>
/// This behavior attaches an event handler to listen for clicks on <see cref="GridViewColumnHeader"/>
/// elements.  When clicked, the list is sorted by the column whose header was clicked.
/// </remarks>
public static class ListViewSorting
{
	#region SortProperty
	public static string GetSortProperty(DependencyObject obj)
		=> (string)obj?.GetValue(SortPropertyProperty);
	public static void SetSortProperty(DependencyObject obj, string value)
		=> obj.SetValue(SortPropertyProperty, value);
	public static readonly DependencyProperty SortPropertyProperty =
		DependencyProperty.RegisterAttached("SortProperty", typeof(string), typeof(ListViewSorting), new PropertyMetadata(null));
	#endregion
	#region IsSortingEnabled
	public static bool GetIsSortingEnabled(DependencyObject obj)
		=> (bool)obj.GetValue(IsSortingEnabledProperty);
	public static void SetIsSortingEnabled(DependencyObject obj, bool value)
		=> obj.SetValue(IsSortingEnabledProperty, value);
	public static readonly DependencyProperty IsSortingEnabledProperty =
		DependencyProperty.RegisterAttached("IsSortingEnabled", typeof(bool), typeof(ListViewSorting), new PropertyMetadata(false, OnIsSortingEnabledChanged));
	#endregion

	private static void ColumnHeader_Click(object o, RoutedEventArgs e)
	{
		var lvw = (ListView)o;
		ListSortInfo sortInfo = GetSortInfo(lvw);

		var col = e.OriginalSource as GridViewColumnHeader;
		if (col != null)
		{
			var sortProp = GetSortProperty(col.Column);
			if (sortProp == null)
			{
				// Try to infer from binding
				var binding = col.Column?.DisplayMemberBinding as Binding;
				sortProp = binding?.Path?.Path;
			}

			if (sortProp != null)
			{
				// sortProp isn't verified.  If it's invalid, no error occurs, but sorting won't work

				if (sortInfo.sortPropName == sortProp)
					sortInfo.descending = !sortInfo.descending;
				else
				{
					sortInfo.sortPropName = sortProp;
					sortInfo.descending = false;
				}

				var view = lvw.ItemsSource as ICollectionView;
				if (view == null)
					view = CollectionViewSource.GetDefaultView(lvw.ItemsSource);

				if (view != null)
				{
					view.SortDescriptions.Clear();
					view.SortDescriptions.Add(new SortDescription(sortProp, sortInfo.descending ? ListSortDirection.Descending : ListSortDirection.Ascending));
				}
			}

			e.Handled = true;
		}
	}

	private static readonly RoutedEventHandler clickHandler = new RoutedEventHandler(ColumnHeader_Click);
	private static void OnIsSortingEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var lvw = d as ListView;
		if (lvw != null)
			if ((bool)e.NewValue)
			{
				ListSortInfo sortInfo = new ListSortInfo();
				SetSortInfo(lvw, sortInfo);

				lvw.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, clickHandler);
			}
			else
				lvw.RemoveHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, clickHandler);
	}

	/// <summary>
	/// Tracks list sort information for a <see cref="ListView"/>.
	/// </summary>
	class ListSortInfo
	{
		/// <summary>
		/// Name of last column clicked
		/// </summary>
		internal string? sortPropName;
		/// <summary>
		/// <c>true</c> if sorting in descending order
		/// </summary>
		internal bool descending;
	}

	private static ListSortInfo GetSortInfo(DependencyObject obj)
		=> (ListSortInfo)obj.GetValue(SortInfoProperty);
	private static void SetSortInfo(DependencyObject obj, ListSortInfo value)
		=> obj.SetValue(SortInfoProperty, value);
	private static readonly DependencyProperty SortInfoProperty =
		DependencyProperty.RegisterAttached("SortInfo", typeof(ListSortInfo), typeof(ListViewSorting), new PropertyMetadata(null));


}
