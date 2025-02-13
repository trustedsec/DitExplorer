using DitExplorer.Ntds;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer;

public interface IDirectoryView
{
	IDirectory Directory { get; }
}

public interface IDirectoryNode
{
	IDirectoryObject Object { get; }
	IDirectoryView DirectoryView { get; }

}
