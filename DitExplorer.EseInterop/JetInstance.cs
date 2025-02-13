using Microsoft.Isam.Esent.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.EseInterop;
public partial class JetInstance
{
	private JET_INSTANCE _inst;

	public static JetInstance Initialize()
	{
		JetInstance instance = new JetInstance();
		Api.JetInit(ref instance._inst);
		return instance;
	}

	public JetSession BeginSession()
	{
		Api.JetBeginSession(this._inst, out var sesid, null, null);
		return new JetSession(sesid);
	}
}
partial class JetInstance : IDisposable
{
	private bool disposedValue;

	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			Api.JetTerm(this._inst);

			disposedValue = true;
		}
	}

	// // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
	// ~JetInstance()
	// {
	//     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
	//     Dispose(disposing: false);
	// }

	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
