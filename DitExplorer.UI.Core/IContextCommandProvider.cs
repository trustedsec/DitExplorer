using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DitExplorer.UI
{
	public class CommandContext
	{
		internal CommandContext(
			ContextMenu menu,
			object[] items,
			object? parameter
			)
		{
			Menu = menu;
			Items = items;
			Parameter = parameter;
		}

		public ContextMenu Menu { get; }
		public object[] Items { get; }
		public object? Parameter { get; }
	}

	public interface IContextCommandProvider
	{
		void GetContextCommands(
			CommandContext context,
			FrameworkElement? target,
			DependencyObject? source
			);
	}
}
