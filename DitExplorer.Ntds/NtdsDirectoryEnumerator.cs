using DitExplorer.EseInterop;
using Microsoft.Isam.Esent.Interop;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.Ntds;

public class DirectoryEnumerable : IEnumerable<NtdsDirectoryObject>
{
	private readonly NtdsDirectory _dir;
	private readonly Func<NtdsDirectory, JetCursor> _cursorFactory;

	internal DirectoryEnumerable(NtdsDirectory dir, Func<NtdsDirectory, JetCursor> cursorFactory)
	{
		_dir = dir;
		_cursorFactory = cursorFactory;
	}

	public IEnumerator<NtdsDirectoryObject> GetEnumerator()
	{
		return new NtdsDirectoryEnumerator(_dir, _cursorFactory(_dir));
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class NtdsDirectoryEnumerator : IEnumerator<NtdsDirectoryObject>
{
	private readonly NtdsDirectory _dir;
	private readonly JetCursor _cursor;

	internal NtdsDirectoryEnumerator(NtdsDirectory dir, JetCursor cursor)
	{
		_dir = dir;
		_cursor = cursor;
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

		Current = _dir.GetObjectFromRecord(_cursor);
		_cursor.MoveNext();

		return true;
	}

	/// <inheritdoc/>
	public void Reset()
		=> throw new NotSupportedException();
}