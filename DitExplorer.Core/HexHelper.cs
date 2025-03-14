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
	public static string ToHexString(
		this byte[]? bytes,
		HexStringOptions options = HexStringOptions.None,
		string separator = " ")
	{
		if (bytes is null)
			return string.Empty;

		string format = (0 != (options & HexStringOptions.Lowercase)) ? "x2" : "X2";
		// TODO: This could be a bit more efficient
		return string.Join(separator, bytes.Select(r => r.ToString(format)));
	}

	public static bool IsHexChar(this char c)
		=> (char.IsDigit(c) || (uint)(c - 'a') < 6U || (uint)(c - 'A') < 6U);
	public static int TryParseHexChar(char c)
	{
		if ((uint)(c - '0') < 10)
			return c - '0';
		else if ((uint)(c - 'a') <= 'f' - 'a')
			return c - 'a' + 10;
		else if ((uint)(c - 'A') <= 'F' - 'A')
			return c - 'A' + 10;
		else
			return -1;
	}
	public static byte[]? TryParseHexString(this string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return null;
		}
		else
		{
			if (value.Length != 32)
			{
				return null;
			}

			byte[] bytes = new byte[16];
			for (int i = 0; i < value.Length; i += 2)
			{
				int d1 = TryParseHexChar(value[i]);
				int d2 = TryParseHexChar(value[i + 1]);
				if (d1 < 0 || d2 < 0)
				{
					return null;
				}
				else
					bytes[i / 2] = (byte)((d1 << 4) | d2);
			}
			return bytes;
		}
	}
	public static byte[]? ParseHexString(this string value)
	{
		if (value == null)
		{
			throw new ArgumentNullException(nameof(value));
		}
		else
		{
			if ((value.Length % 2) != 0)
				throw new ArgumentException("The string must contain exactly 32 hex digits.", nameof(value));

			byte[] bytes = new byte[value.Length / 2];
			ParseHexStringInto(value, bytes);
			return bytes;
		}
	}

	public static int ParseHexStringInto(this string value, Span<byte> bytes)
	{
		if (bytes.Length < (value.Length / 2))
			throw new ArgumentException("The array is too small.", nameof(bytes));

		int i;
		for (i = 0; i < value.Length; i += 2)
		{
			int d1 = TryParseHexChar(value[i]);
			int d2 = TryParseHexChar(value[i + 1]);
			if (d1 < 0 || d2 < 0)
				throw new ArgumentException($"The string contains an invalid digit at position {i}", nameof(value));
			else
				bytes[(i / 2)] = (byte)((d1 << 4) | d2);
		}
		return i;
	}
}
