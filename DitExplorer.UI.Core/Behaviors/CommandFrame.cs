using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace DitExplorer.UI.Behaviors;
public static class CommandFrame
{
	public static ICommandHandler GetCommandHandler(DependencyObject obj)
	{
		return (ICommandHandler)obj.GetValue(CommandHandlerProperty);
	}

	public static void SetCommandHandler(DependencyObject obj, ICommandHandler value)
	{
		obj.SetValue(CommandHandlerProperty, value);
	}

	// Using a DependencyProperty as the backing store for CommandHandler.  This enables animation, styling, binding, etc...
	public static readonly DependencyProperty CommandHandlerProperty =
		DependencyProperty.RegisterAttached("CommandHandler", typeof(ICommandHandler), typeof(CommandFrame), new PropertyMetadata(null, OnHandlerChanged));

	private static void OnHandlerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var elem = d as FrameworkElement;
		if (elem != null && e.NewValue != null)
		{
			var handler = (ICommandHandler)e.NewValue;
			CommandManager.AddCanExecuteHandler(elem, HandleCanExecute);
			CommandManager.AddExecutedHandler(elem, HandleExecuted);

			if (elem.IsLoaded)
				handler.OnLoaded(elem);
			else
				elem.Loaded += Elem_Loaded;

			elem.Unloaded += Elem_Unloaded;
		}
		// TODO: Detach when set to null
	}

	private static void Elem_Loaded(object sender, RoutedEventArgs e)
	{
		var elem = (FrameworkElement)sender;
		var handler = GetCommandHandler(elem);
		handler?.OnLoaded(elem);
	}

	private static void Elem_Unloaded(object sender, RoutedEventArgs e)
	{
		var elem = (FrameworkElement)sender;
		var handler = GetCommandHandler(elem);
		handler?.OnUnloaded();
	}

	private static List<ICommandHandler> _globalHooks = new List<ICommandHandler>();
	public static void AddGlobalCommandHandler(ICommandHandler handler)
	{
		if (handler is null) throw new ArgumentNullException(nameof(handler));
		_globalHooks.Add(handler);
	}

	private static void HandleCanExecute(object sender, CanExecuteRoutedEventArgs e)
	{
		if (e.Command == ApplicationCommands.Close && sender is Window wnd)
		{
			var handler = GetCommandHandler((DependencyObject)sender);
			e.CanExecute = handler?.CanClose() ?? false;
			e.Handled = true;
		}
		else
		{
			var handler = GetCommandHandler((DependencyObject)sender);
			handler?.HandleCanExecute(e);
		}

		if (!e.Handled)
			foreach (var hook in _globalHooks)
			{
				hook.HandleCanExecute(e);
				if (e.Handled)
					break;
			}
	}

	private static void HandleExecuted(object sender, ExecutedRoutedEventArgs e)
	{
		if (e.Command == ApplicationCommands.Close && sender is Window wnd)
		{
			var handler = GetCommandHandler((DependencyObject)sender);
			var canClose = handler?.CanClose() ?? false;
			if (canClose)
				wnd.Close();
		}
		else
		{
			var handler = GetCommandHandler((DependencyObject)sender);
			handler?.HandleExecute(e);
		}

		if (!e.Handled)
			foreach (var hook in _globalHooks)
			{
				hook.HandleExecute(e);
				if (e.Handled)
					break;
			}
	}
}
