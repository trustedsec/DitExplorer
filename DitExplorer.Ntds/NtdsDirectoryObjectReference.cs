using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.Ntds;
public class NtdsDirectoryObjectReference
{
	internal NtdsDirectoryObjectReference(NtdsDirectory directory, int dnt)
	{
		Directory = directory;
		Dnt = dnt;
	}

	public NtdsDirectory Directory { get; }
	public int Dnt { get; }

	private NtdsDirectoryObject? _target;
	public NtdsDirectoryObject Target => (this._target ??= this.Directory.GetByDnt(this.Dnt));

	public override string ToString() => this.Target?.DistinguishedName;
}
