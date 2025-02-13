using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DitExplorer.UI.Behaviors;

public static class FlexGrid
{


	public static PropertyDescriptor? GetDisplayProperty(DependencyObject obj)
		=> (PropertyDescriptor?)obj.GetValue(DisplayPropertyProperty);
	public static void SetDisplayProperty(DependencyObject obj, PropertyDescriptor? value)
		=> obj.SetValue(DisplayPropertyProperty, value);
	public static readonly DependencyProperty DisplayPropertyProperty =
		DependencyProperty.RegisterAttached("DisplayProperty", typeof(PropertyDescriptor), typeof(FlexGrid), new PropertyMetadata(null));
}
