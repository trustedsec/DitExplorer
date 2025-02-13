using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace DitExplorer.UI;

/// <summary>
/// Base class for viewmodel implementations.
/// </summary>
/// <remarks>
/// This class inheris <see cref="Notifier"/> for property-change notifications and
/// implements <see cref="ICommandHandler"/>.  Register command handlers by calling 
/// <see cref="RegisterCommand(ICommand, Action, Func{bool}?)"/> or one of its overloads.
/// </remarks>
public class ViewModel : Notifier, ICommandHandler
{
	protected ViewModel()
	{
	}

	#region Commands
	class CommandHandlerEntry
	{
		internal Action<object> handler;
		internal Func<object, bool> canExecute;
	}

	Dictionary<ICommand, CommandHandlerEntry>? _handlers;

	private void AddCommandEntry(ICommand command, CommandHandlerEntry entry)
	{
		(_handlers ??= new Dictionary<ICommand, CommandHandlerEntry>()).Add(command, entry);
	}
	protected void RegisterCommand(ICommand command, Action<object> action, Func<object, bool>? canExecute = null)
	{
		AddCommandEntry(command, new CommandHandlerEntry
		{
			handler = action,
			canExecute = canExecute
		});
	}
	protected void RegisterCommand(ICommand command, Action action, Func<bool>? canExecute = null)
	{
		RegisterCommand(command,
			o => action(),
			canExecute == null ? null : o => canExecute()
			);
	}
	protected void RegisterCommand<T>(ICommand command, Action<T> action)
		where T : class
		=> RegisterCommand(command, action, null);
	protected void RegisterCommand<T>(ICommand command, Action<T> action, Func<T, bool>? canExecute, bool includeNull = false)
		where T : class
	{
		RegisterCommand(command,
			o =>
			{
				T? param = o as T;
				if (param != null || o is null && includeNull)
					action(param);
			},
			canExecute == null ? null : o => o is T typed && canExecute(typed)
			);
	}

	protected virtual bool CanClose()
	{
		return true;
	}
	bool ICommandHandler.CanClose() => CanClose();

	void ICommandHandler.HandleCanExecute(CanExecuteRoutedEventArgs e)
	{
		if (_handlers != null && _handlers.TryGetValue(e.Command, out var entry))
		{
			e.CanExecute = entry.canExecute?.Invoke(e.Parameter) ?? true;
			e.Handled = true;
		}
	}

	void ICommandHandler.HandleExecute(ExecutedRoutedEventArgs e)
	{
		try
		{
			if (_handlers != null && _handlers.TryGetValue(e.Command, out var entry))
			{
				entry.handler(e.Parameter);
				e.Handled = true;
			}
		}
		catch (Exception ex)
		{
			this.ReportError("An error occurred while executing the command: " + ex.Message, "DIT Explorer", ex);
		}
	}
	#endregion

	protected void ReportError(string message, string title, Exception ex)
	{
		// TODO: Show a better error interface
		if (Window == null)
			MessageBox.Show(
				message + "\r\n\r\n" + ex.Message,
				title,
				MessageBoxButton.OK,
				MessageBoxImage.Error
				);
		else
			MessageBox.Show(
				Window,
				message + "\r\n\r\n" + ex.Message,
				title,
				MessageBoxButton.OK,
				MessageBoxImage.Error
				);
	}

	protected virtual void OnViewUnloaded()
	{

	}
	void ICommandHandler.OnUnloaded()
		=> OnViewUnloaded();

	protected FrameworkElement? ViewElement { get; private set; }
	public Window? Window { get; private set; }

	protected virtual void OnViewLoaded(FrameworkElement viewElement)
	{
	}
	void ICommandHandler.OnLoaded(FrameworkElement element)
	{
		ViewElement = element;
		Window = Window.GetWindow(element);
		OnViewLoaded(element);
	}
}
