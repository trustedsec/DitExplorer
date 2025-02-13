using DitExplorer.EseInterop;
using Microsoft.Isam.Esent.Interop;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.Ntds;

public class NtdsDirectoryLinkEnumerable : IEnumerable<NtdsDirectoryObject>
{
	private readonly NtdsDirectory _dir;
	private readonly Func<NtdsDirectory, JetCursor> _cursorFactory;
	private readonly JET_COLUMNID _dntColid;

	internal NtdsDirectoryLinkEnumerable(NtdsDirectory dir, Func<NtdsDirectory, JetCursor> cursorFactory, JET_COLUMNID dntColid)
	{
		_dir = dir;
		_cursorFactory = cursorFactory;
		_dntColid = dntColid;
	}

	public IEnumerator<NtdsDirectoryObject> GetEnumerator()
	{
		return new NtdsDirectoryLinkEnumerator(_dir, _cursorFactory(_dir), this._dntColid);
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class NtdsDirectoryLinkEnumerator : IEnumerator<NtdsDirectoryObject>
{
	private readonly NtdsDirectory _dir;
	private readonly JetCursor _cursor;
	private readonly JET_COLUMNID _dntColid;

	internal NtdsDirectoryLinkEnumerator(NtdsDirectory dir, JetCursor cursor, JET_COLUMNID dntColid)
	{
		_dir = dir;
		_cursor = cursor;
		_dntColid = dntColid;
		Current = null;
	}

	/// <inheritdoc/>
	public NtdsDirectoryObject? Current { get; private set; }

	/// <inheritdoc/>
	object IEnumerator.Current => Current;

	/// <inheritdoc/>
	public void Dispose()
	{
		_cursor.Dispose();
	}
	/// <inheritdoc/>
	public bool MoveNext()
	{
		// This method is called before retrieving an item.  However,
		// The cursor is already positioned at the "next" item.

		if (!_cursor.HasCurrentRecord)
		{
			Current = null;
			return false;
		}

		var dnt = _cursor.ReadInt32(this._dntColid).Value;
		Current = _dir.GetByDnt(dnt);
		_cursor.MoveNext();

		return true;
	}

	/// <inheritdoc/>
	public void Reset()
		=> throw new NotSupportedException();
}