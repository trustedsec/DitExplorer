using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DitExplorer.UI.WpfApp
{
	public class ContextCommandInfo
	{
		public ContextCommandInfo(ICommand command, object? parameter, string displayText)
		{
			Command = command;
			Parameter = parameter;
			DisplayText = displayText;
		}

		public ICommand Command { get; }
		public object? Parameter { get; }
		public string DisplayText { get; }
	}
}
