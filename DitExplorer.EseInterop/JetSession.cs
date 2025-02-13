using Microsoft.Isam.Esent.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace DitExplorer.EseInterop;

public enum OpenDatabaseOptions
{
	None = 0,
	ReadOnly = 1,
}

public partial class JetSession
{
	internal readonly JET_SESID sesid;

	internal JetSession(JET_SESID sesid)
	{
		this.sesid = sesid;
	}

	public static JetSession Begin()
	{
		Api.JetBeginSession(JET_INSTANCE.Nil, out var sesid, null, null);
		return new JetSession(sesid);
	}

	public JetDatabase AttachAndOpen(string fileName, OpenDatabaseOptions options)
	{
		this.VerifyNotDisposed();

		AttachDatabaseGrbit attachFlags = (0 != (options & OpenDatabaseOptions.ReadOnly)) ? AttachDatabaseGrbit.ReadOnly : AttachDatabaseGrbit.None;
		Api.JetAttachDatabase(this.sesid, fileName, attachFlags);

		try
		{
			OpenDatabaseGrbit openFlags = (0 != (options & OpenDatabaseOptions.ReadOnly)) ? OpenDatabaseGrbit.ReadOnly : OpenDatabaseGrbit.None;
			Api.JetOpenDatabase(this.sesid, fileName, null, out var dbid, openFlags);
			var db = new JetDatabase(this.sesid, dbid, fileName);
			fileName = null;
			return db;
		}
		finally
		{
			if (fileName != null)
				Api.JetDetachDatabase(this.sesid, fileName);
		}
	}


	public JetCursor IntersectAll(JetCursor[] cursors)
	{
		this.VerifyNotDisposed();

		if (cursors == null || cursors.Length == 0 || (Array.IndexOf(cursors, null) >= 0))
			throw new ArgumentNullException(nameof(cursors));
		if (Array.FindIndex(cursors, r => r.sesid != this.sesid) >= 0)
			throw new ArgumentException("One or more of the cursors belong to another session.  This is not allowed.");
		if (Array.FindIndex(cursors, r => r.IsDisposed) >= 0)
			throw new ArgumentException("One or more of the cursors are disposed.");

		var ranges = Array.ConvertAll(cursors, r => new JET_INDEXRANGE() { tableid = r.tableId, grbit = IndexRangeGrbit.RecordInIndex });
		Api.JetIntersectIndexes(this.sesid, ranges, ranges.Length, out var reclist, IntersectIndexesGrbit.None);
		using (JetCursor curIntersect = new JetCursor(this.sesid, reclist.tableid))
		{
			throw new NotImplementedException();
		}
	}
}

partial class JetSession : IDisposable
{
	private bool disposedValue;
	protected void VerifyNotDisposed()
	{
		if (this.disposedValue)
			throw new ObjectDisposedException(Messages.JetObjectDisposedMessage);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			Api.JetEndSession(this.sesid, EndSessionGrbit.None);
			disposedValue = true;
		}
	}

	// // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
	~JetSession()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: false);
	}

	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
