using System.Windows;
using System.Windows.Input;

namespace DitExplorer.UI;

public interface ICommandHandler
{
	bool CanClose();
	void HandleCanExecute(CanExecuteRoutedEventArgs e);
	void HandleExecute(ExecutedRoutedEventArgs e);
	void OnLoaded(FrameworkElement element);
	void OnUnloaded();
}