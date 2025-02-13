using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace DitExplorer.UI.Behaviors;

public static class FocusHelper
{


	public static IInputElement? GetDeferredFocusElement(DependencyObject obj)
	{
		return (IInputElement?)obj.GetValue(DeferredFocusElementProperty);
	}

	public static void SetDeferredFocusElement(DependencyObject obj, IInputElement? value)
	{
		obj.SetValue(DeferredFocusElementProperty, value);
	}

	// Using a DependencyProperty as the backing store for DeferredFocusElement.  This enables animation, styling, binding, etc...
	public static readonly DependencyProperty DeferredFocusElementProperty =
		DependencyProperty.RegisterAttached("DeferredFocusElement", typeof(IInputElement), typeof(FocusHelper), new PropertyMetadata(null, OnDeferredFocusElementChanged));

	private static void OnDeferredFocusElementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var elem = e.NewValue as FrameworkElement;
		if (elem != null)
			if (elem.IsLoaded)
				FocusManager.SetFocusedElement(d, elem);
			else
				elem.Loaded += Elem_Loaded;
	}

	private static void Elem_Loaded(object sender, RoutedEventArgs e)
	{
		var obj = (DependencyObject)sender;
		var scope = FocusManager.GetFocusScope(obj);
		if (scope != null)
			Dispatcher.CurrentDispatcher.BeginInvoke(() =>
			{
				FocusManager.SetFocusedElement(scope, (IInputElement)sender);
			}, DispatcherPriority.Input);
	}
}
