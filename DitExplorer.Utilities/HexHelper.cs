using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer;

[Flags]
public enum HexStringOptions
{
	None = 0,
	Lowercase = 1,
	Uppercase = 0,
}
public static class HexHelper
{
	public static string? ToHexString(this byte[]? bytes, HexStringOptions options = HexStringOptions.None, string separator = " ")
	{
		if (bytes is null)
			return null;

		string format = (0 != (options & HexStringOptions.Lowercase)) ? "x2" : "X2";
		// TODO: This could be a bit more efficient
		return string.Join(separator, bytes.Select(r => r.ToString(format)));
	}
}
