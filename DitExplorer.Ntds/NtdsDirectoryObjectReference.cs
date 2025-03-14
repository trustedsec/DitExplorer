using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.Ntds;
public class NtdsDirectoryObjectReference : IEquatable<NtdsDirectoryObjectReference>
{
	internal NtdsDirectoryObjectReference(NtdsDirectory directory, int dnt)
	{
		Directory = directory;
		Dnt = dnt;
	}
	internal NtdsDirectoryObjectReference(NtdsDirectoryObject target)
	{
		Debug.Assert(target is not null);

		Directory = target.Directory;
		Dnt = target.Dnt;
		_target = target;
	}

	public override int GetHashCode()
		=> this.Dnt;

	public NtdsDirectory Directory { get; }
	public int Dnt { get; }

	private NtdsDirectoryObject? _target;
	public NtdsDirectoryObject Target => (this._target ??= this.Directory.GetByDnt(this.Dnt));

	public override string ToString() => this.Target?.DistinguishedName;

	public sealed override bool Equals(object? obj)
		=> (obj is NtdsDirectoryObjectReference other) && this.Equals(other);
	public virtual bool Equals(NtdsDirectoryObjectReference? other)
		=> (other != null) && other.Dnt == this.Dnt;
}

public class NtdsDirectoryObjectReference<T> : NtdsDirectoryObjectReference
{
	internal NtdsDirectoryObjectReference(NtdsDirectory directory, int dnt, T tag)
		: base(directory, dnt)
	{
		Tag = tag;
	}

	public T? Tag { get; }

	public override string ToString() => $"{this.Target?.DistinguishedName} ({this.Tag})";
}
