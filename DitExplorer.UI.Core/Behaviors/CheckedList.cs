using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DitExplorer.UI.Behaviors;
public static class CheckedList
{
	#region IsCheckingEnabled
	public static bool GetIsCheckingEnabled(DependencyObject obj)
		=> (bool)obj.GetValue(InCheckingEnabledProperty);
	public static void SetIsCheckingEnabled(DependencyObject obj, bool value)
		=> obj.SetValue(InCheckingEnabledProperty, value);
	public static readonly DependencyProperty InCheckingEnabledProperty =
		DependencyProperty.RegisterAttached("IsCheckingEnabled", typeof(bool), typeof(CheckedList), new PropertyMetadata(false, OnEnabledChanged));

	private static readonly object? NoValue = new object();
	private static object? TryGetPropertyOnItem(object item, string propertyName)
	{
		PropertyDescriptor? prop = TryGetProperty(item, propertyName);
		if (prop != null)
		{
			var value = prop.GetValue(item);
			return value;
		}
		else
			return NoValue;
	}

	private static PropertyDescriptor? TryGetProperty(object item, string propertyName)
	{
		var props = TypeDescriptor.GetProperties(item);
		var prop = props.Find(propertyName, false);
		return prop;
	}

	private static void TrySetPropertyOnItem(object item, string propertyName, object? value)
	{
		PropertyDescriptor? prop = TryGetProperty(item, propertyName);
		if (prop != null)
			prop.SetValue(item, value);
	}

	private static void ListView_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Space)
		{
			ListView lvw = (ListView)sender;
			var propName = GetIsCheckedProperty(lvw);

			if (propName != null)
			{
				// Check the current state

				bool anyChecked = false;
				bool anyUnchecked = false;

				foreach (var item in lvw.SelectedItems)
					if (item != null)
					{
						var value = TryGetPropertyOnItem(item, propName) as bool?;
						if (value.HasValue)
						{
							anyChecked |= value.Value;
							anyUnchecked |= !value.Value;
						}
					}

				// If any items are unchecked, the action is to check the selected items
				bool newValue = anyUnchecked;
				foreach (var item in lvw.SelectedItems)
					if (item != null)
						TrySetPropertyOnItem(item, propName, newValue);

				e.Handled = true;
			}
		}
	}

	private static void ListViewItem_DoubleClick(object sender, MouseButtonEventArgs e)
	{
		ListView lvw = (ListView)sender;
		var propName = GetIsCheckedProperty(lvw);

		if (propName != null)
		{
			// Check the current state

			object item = lvw.SelectedItem;
			if (item != null)
			{
				var isChecked = TryGetPropertyOnItem(item, propName) as bool?;
				if (isChecked.HasValue)
					TrySetPropertyOnItem(item, propName, !isChecked.Value);
			}

			e.Handled = true;
		}
	}

	private static readonly KeyEventHandler keyDownHandler = new KeyEventHandler(ListView_KeyDown);
	private static readonly MouseButtonEventHandler doubleClickHandler = new MouseButtonEventHandler(ListViewItem_DoubleClick);

	private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var lvw = d as ListView;
		if (lvw != null)
			if ((bool)e.NewValue)
			{   // If the list also has text searching enabled, it'll eat this event first
				lvw.AddHandler(UIElement.PreviewKeyDownEvent, keyDownHandler);
				lvw.AddHandler(Control.MouseDoubleClickEvent, doubleClickHandler);
			}
			else
			{
				lvw.RemoveHandler(UIElement.PreviewKeyDownEvent, keyDownHandler);
				lvw.RemoveHandler(Control.MouseDoubleClickEvent, doubleClickHandler);
			}
	}
	#endregion

	#region CheckBoxElementName
	public static string? GetIsCheckedProperty(DependencyObject obj)
		=> (string?)obj.GetValue(IsCheckedPropertyProperty);
	public static void SetIsCheckedProperty(DependencyObject obj, string? value)
		=> obj.SetValue(IsCheckedPropertyProperty, value);
	public static readonly DependencyProperty IsCheckedPropertyProperty =
		DependencyProperty.RegisterAttached("IsCheckedProperty", typeof(string), typeof(CheckedList), new PropertyMetadata(null));
	#endregion
}
