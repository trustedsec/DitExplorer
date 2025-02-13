using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.Ntds;

[Serializable]
public class DirectoryObjectNotFoundException : Exception
{
	public DirectoryObjectNotFoundException() { }
	public DirectoryObjectNotFoundException(string message) : base(message) { }
	public DirectoryObjectNotFoundException(string message, Exception inner) : base(message, inner) { }
	protected DirectoryObjectNotFoundException(
	  System.Runtime.Serialization.SerializationInfo info,
	  System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
}