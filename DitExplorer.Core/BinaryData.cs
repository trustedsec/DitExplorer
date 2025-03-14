using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer;

/// <summary>
/// Represents a value containing binary data.
/// </summary>
/// <remarks>
/// This class encapsulates a byte array and implements <see cref="ToString"/> to print the array as hex,
/// and overrides <see cref="GetHashCode"/> and implements <see cref="IComparable"/> to implement
/// equality checks.
/// </remarks>
public class BinaryData : IComparable<BinaryData>
{
	public BinaryData(byte[] bytes)
	{
		if (bytes is null) throw new ArgumentNullException(nameof(bytes));
		Bytes = bytes;
	}

	public byte[] Bytes { get; }

	public static object? Parse(string text)
	{
		throw new NotImplementedException();
	}

	public int CompareTo(BinaryData? other)
	{
		throw new NotImplementedException();
	}

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		int hash = 0;
		foreach (var b in this.Bytes)
		{
			hash = HashCode.Combine(hash, b);
		}
		return hash;
	}

	/// <inheritdoc/>
	public override string ToString()
		=> this.Bytes.ToHexString();
}
