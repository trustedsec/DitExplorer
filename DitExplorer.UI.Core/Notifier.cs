using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DitExplorer.UI;

/// <summary>
/// Implements <see cref="INotifyPropertyChanged"/>
/// </summary>
/// <remarks>
/// Call <see cref="NotifyIfChanged{T}(ref T, T, string?)"/> from a property setter
/// to set the backing field and detect if its value has changed.
/// </remarks>
public class Notifier : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;
	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	protected bool NotifyIfChanged<T>(ref T field, T newValue, [CallerMemberName] string? propertyName = null)
	{
		if (!EqualityComparer<T>.Default.Equals(field, newValue))
		{
			field = newValue;
			OnPropertyChanged(propertyName);
			return true;
		}
		else
			return false;
	}
}
