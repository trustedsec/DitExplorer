using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DitExplorer.UI.Behaviors
{
	public class ContextCommandService
	{
		public static IContextCommandProvider? GetCommandProvider(DependencyObject obj)
			=> (IContextCommandProvider?)obj.GetValue(CommandProviderProperty);
		public static void SetCommandProvider(DependencyObject obj, IContextCommandProvider? value)
			=> obj.SetValue(CommandProviderProperty, value);
		public static readonly DependencyProperty CommandProviderProperty =
			DependencyProperty.RegisterAttached("CommandProvider", typeof(IContextCommandProvider), typeof(ContextCommandService), new PropertyMetadata(null, OnProviderChanged));

		public static object? GetCommandParameter(DependencyObject obj)
			=> (object?)obj.GetValue(CommandParameterProperty);
		public static void SetCommandParameter(DependencyObject obj, object? value)
			=> obj.SetValue(CommandParameterProperty, value);
		public static readonly DependencyProperty CommandParameterProperty =
			DependencyProperty.RegisterAttached("CommandParameter", typeof(object), typeof(ContextCommandService), new PropertyMetadata(null));


		#region DefaultCommand
		public static ICommand? GetDefaultCommand(DependencyObject obj)
			=> (ICommand?)obj.GetValue(DefaultCommandProperty);
		public static void SetDefaultCommand(DependencyObject obj, ICommand? value)
			=> obj.SetValue(DefaultCommandProperty, value);
		public static readonly DependencyProperty DefaultCommandProperty =
			DependencyProperty.RegisterAttached("DefaultCommand", typeof(ICommand), typeof(ContextCommandService), new PropertyMetadata(null, OnDefaultCommandChanged));

		private static void OnDefaultCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var elem = d as FrameworkElement;
			if (elem != null)
				if (e.OldValue == null)
					elem.PreviewMouseLeftButtonDown += Elem_MouseLeftButtonUp;
				else if (e.NewValue == null)
					elem.PreviewMouseLeftButtonDown -= Elem_MouseLeftButtonUp;
		}



		public static object? GetDefaultCommandParameter(DependencyObject obj)
		{
			return (object?)obj.GetValue(DefaultCommandParameterProperty);
		}

		public static void SetDefaultCommandParameter(DependencyObject obj, object? value)
		{
			obj.SetValue(DefaultCommandParameterProperty, value);
		}

		// Using a DependencyProperty as the backing store for DefaultCommandParameter.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty DefaultCommandParameterProperty =
			DependencyProperty.RegisterAttached("DefaultCommandParameter", typeof(object), typeof(ContextCommandService), new PropertyMetadata(null));



		private static void InvokeDefaultCommand(object sender, RoutedEventArgs e)
		{
			DependencyObject obj = (DependencyObject)sender;
			var cmd = GetDefaultCommand(obj);
			if (cmd != null)
			{
				var param = GetDefaultCommandParameter(obj);
				if (cmd is RoutedUICommand uicmd)
					uicmd.Execute(param, e.Source as IInputElement);
				else
					cmd.Execute(param);
				e.Handled = true;
			}
		}
		#endregion

		private static bool IsFunctionKey(Key key)
		{
			return key >= Key.F1 && key <= Key.F24;
		}

		private static void Elem_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
				InvokeDefaultCommand(sender, e);
			else if (
				e.KeyboardDevice.Modifiers != ModifierKeys.None
				|| IsFunctionKey(e.Key)
				)
			{
				var elem = (FrameworkElement)sender;
				var prov = GetCommandProvider(elem);
				if (prov != null)
				{
					ContextMenu menu = new ContextMenu();
					CommandContext context = CreateCommandContext(e, elem, menu);
					try
					{
						prov.GetContextCommands(context, elem, (DependencyObject)e.OriginalSource);
					}
					catch
					{
						// Silently fail
					}

					ICommand? matchedCmd = null;
					object? param = null;
					IInputElement? target = null;
					if (menu.Items.Count > 0)
						foreach (var item in menu.Items)
							if (item is MenuItem menuItem)
							{
								var cmd = menuItem.Command;
								if (cmd is RoutedUICommand uicmd && uicmd.InputGestures?.Count > 0)
									if (uicmd.InputGestures[0].Matches(sender, e))
									{
										matchedCmd = uicmd;
										param = menuItem.CommandParameter;
										target = menuItem.CommandTarget;
										break;
									}
							}

					if (matchedCmd != null)
					{
						e.Handled = true;

						if (matchedCmd is RoutedUICommand uicmd)
							uicmd.Execute(param, target);
						else
							matchedCmd.Execute(param);
					}
				}
			}
		}

		private static void Elem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (e.ClickCount == 2)
				InvokeDefaultCommand(sender, e);
		}

		private static void OnProviderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var elem = d as FrameworkElement;
			if (elem != null)
				if (e.OldValue == null)
				{
					elem.ContextMenuOpening += Elem_ContextMenuOpening;
					elem.PreviewKeyDown += Elem_PreviewKeyDown;
					if (elem.ContextMenu == null)
						elem.ContextMenu = new ContextMenu();
				}
				else if (e.NewValue == null)
				{
					elem.ContextMenuOpening -= Elem_ContextMenuOpening;
					elem.PreviewKeyDown += Elem_PreviewKeyDown;
				}
		}

		private static void Elem_ContextMenuOpening(object sender, ContextMenuEventArgs e)
		{
			var elem = (FrameworkElement)sender;
			var prov = GetCommandProvider(elem);
			if (elem != null)
			{
				var context = GetCommandsFor(e, elem, prov);
				if (context == null || context.Menu.Items.Count == 0)
					// Don't show
					e.Handled = true;
				else
				{
					// Let the menu be shown
				}
			}
		}

		private static CommandContext? GetCommandsFor(
			ContextMenuEventArgs e,
			FrameworkElement elem,
			IContextCommandProvider? prov)
		{
			var menu = elem.ContextMenu;
			if (menu != null)
				menu.Items.Clear();
			else
				return null;

			CommandContext context = CreateCommandContext(e, elem, menu);
			try
			{

				menu.Items.Clear();
				prov.GetContextCommands(context, elem, (DependencyObject)e.OriginalSource);

				return context;
			}
			catch
			{
				// Silently fail
				// Prevent context menu from opening
				return null;
			}
		}

		private static CommandContext CreateCommandContext(RoutedEventArgs e, FrameworkElement elem, ContextMenu menu)
		{
			object[] items = Array.Empty<object>();
			var param = GetCommandParameter(elem);
			if (e.OriginalSource is DependencyObject origDO)
			{
				var cont = ItemsControl.ContainerFromElement(null, origDO);
				if (cont != null)
				{
					var owner = ItemsControl.ItemsControlFromItemContainer(cont);
					if (owner != null)
						if (owner is ListView lvw)
							// If the user right-clicks an item not selected, it becomes the selection
							items = lvw.SelectedItems.ToArray();
						else
							// If the user right-clicks a tree node not selected, it doesn't become the selection
							// So choose the item clicked rather than trusting the SelectedItem/s property.
							items = new object[] { owner.ItemContainerGenerator.ItemFromContainer(cont) };
				}
			}
			CommandContext context = new CommandContext(menu, items, param);
			return context;
		}
	}
}
