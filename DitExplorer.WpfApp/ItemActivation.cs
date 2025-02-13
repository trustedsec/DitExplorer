using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DitExplorer.UI.WpfApp
{
	static class ItemActivation
	{


		public static bool GetIsEnabled(DependencyObject obj)
			=> (bool)obj.GetValue(IsEnabledProperty);
		public static void SetIsEnabled(DependencyObject obj, bool value)
			=> obj.SetValue(IsEnabledProperty, value);
		public static readonly DependencyProperty IsEnabledProperty =
			DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(ItemActivation), new PropertyMetadata(false, OnIsEnabledChanged));

		private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var elem = d as FrameworkElement;
			if (elem != null)
				if ((bool)e.NewValue)
				{
					elem.PreviewKeyDown += Elem_PreviewKeyDown;
					elem.MouseLeftButtonDown += Elem_MouseLeftButtonDown;
				}
				else
				{
					elem.PreviewKeyDown += Elem_PreviewKeyDown;
					elem.MouseLeftButtonDown += Elem_MouseLeftButtonDown;
				}
		}

		private static void Elem_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
		}

		private static void Elem_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
		}
	}
}
