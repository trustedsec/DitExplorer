using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer;

public enum ViewTechnology
{
	Wpf = 1,
}

public interface IDirectorySearchHandler
{
	string Caption { get; }
	object? GetView();
	IEnumerable<IDirectoryObject> GetResults();
}

public interface IDirectorySearchProvider
{
	IDirectorySearchHandler GetSearchHandler(
		IDirectoryObject searchRoot,
		ViewTechnology viewTech
		);
}
