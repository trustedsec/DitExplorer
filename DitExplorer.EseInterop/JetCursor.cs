using Microsoft.Isam.Esent;
using Microsoft.Isam.Esent.Interop;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.EseInterop;

/// <summary>
/// Represents an open Jet cursor.
/// </summary>
/// <remarks>
/// The ESE documentation overloads the term `table` to refer to the construct
/// within a database that stores records as well as the in-memory construct that
/// traverses them.  This library uses the term `cursor` for the latter to
/// disambiguate these concepts.
/// </remarks>
/// <seealso cref="JetDatabase.OpenTable(string)"/>
public partial class JetCursor
{
	internal readonly JET_SESID sesid;
	internal readonly JET_TABLEID tableId;

	internal JetCursor(JET_SESID sesid, JET_TABLEID tableId)
	{
		this.sesid = sesid;
		this.tableId = tableId;
		this.HasCurrentRecord = true;
	}

	/// <summary>
	/// Gets a value indicating whether this cursor has a current record.
	/// </summary>
	public bool HasCurrentRecord { get; private set; }

	/// <summary>
	/// Constructs a key.
	/// </summary>
	/// <param name="value"></param>
	/// <param name="options"></param>
	/// <remarks>
	/// Call this method (or another overload) to build a key before calling <see cref="Seek(SeekGrbit)"/> or
	/// <see cref="SetIndexRange(SetIndexRangeGrbit)(string)"/>.
	/// </remarks>
	public void MakeKey(byte[] keyData, MakeKeyGrbit options)
	{
		if (keyData is null || keyData.Length == 0) throw new ArgumentNullException(nameof(keyData));

		this.VerifyNotDisposed();

		Api.JetMakeKey(
			this.sesid,
			this.tableId,
			keyData,
			keyData.Length,
			options
			);
	}
	/// <summary>
	/// Constructs a key.
	/// </summary>
	/// <param name="value"></param>
	/// <param name="options"></param>
	/// <remarks>
	/// Call this method (or another overload) to build a key before calling <see cref="Seek(SeekGrbit)"/> or
	/// <see cref="SetIndexRange(SetIndexRangeGrbit)(string)"/>.
	/// </remarks>
	public void MakeKey(int value, MakeKeyGrbit options) => this.MakeKey(BitConverter.GetBytes(value), options);
	/// <summary>
	/// Constructs a key.
	/// </summary>
	/// <param name="value"></param>
	/// <param name="options"></param>
	/// <remarks>
	/// Call this method (or another overload) to build a key before calling <see cref="Seek(SeekGrbit)"/> or
	/// <see cref="SetIndexRange(SetIndexRangeGrbit)(string)"/>.
	/// </remarks>
	public void MakeKey(long value, MakeKeyGrbit options) => this.MakeKey(BitConverter.GetBytes(value), options);
	/// <summary>
	/// Constructs a key.
	/// </summary>
	/// <param name="value"></param>
	/// <param name="options"></param>
	/// <remarks>
	/// Call this method (or another overload) to build a key before calling <see cref="Seek(SeekGrbit)"/> or
	/// <see cref="SetIndexRange(SetIndexRangeGrbit)(string)"/>.
	/// </remarks>
	public void MakeKey(string value, MakeKeyGrbit options) => this.MakeKey(Encoding.Unicode.GetBytes(value), options);

	/// <summary>
	/// Seeks to the record with the current key.
	/// </summary>
	/// <param name="seekOptions">A <see cref="SeekGrbit"/> value</param>
	/// <returns><see langword="true"/> if a record is found; otherwise, <see langword="false"/>.</returns>
	/// <remarks>
	/// Call this method (or another overload) to build a key before calling <see cref="Seek(SeekGrbit)"/> or
	/// a key before calling this method.
	/// <para>
	/// This method updates <see cref="HasCurrentRecord"/>.
	/// </para>
	/// </remarks>
	public bool Seek(SeekGrbit seekOptions = SeekGrbit.SeekEQ)
	{
		this.VerifyNotDisposed();

		this.HasCurrentRecord = Api.TrySeek(this.sesid, this.tableId, seekOptions);
		return this.HasCurrentRecord;
	}
	/// <summary>
	/// Sets the index range limit.
	/// </summary>
	/// <param name="options">A <see cref="SetIndexRangeGrbit"/> value</param>
	/// <returns><see langword="true"/> if a record is found; otherwise, <see langword="false"/>.</returns>
	/// <remarks>
	/// Call <see cref="MakeKey(byte[], MakeKeyGrbit)"/> or one of its overloads to build
	/// a key before calling this method.
	/// <para>
	/// This method updates <see cref="HasCurrentRecord"/>.
	/// </para>
	/// </remarks>
	public bool SetIndexRange(SetIndexRangeGrbit options)
	{
		this.VerifyNotDisposed();

		this.HasCurrentRecord = Api.TrySetIndexRange(this.sesid, this.tableId, options);
		return this.HasCurrentRecord;
	}
	/// <summary>
	/// Sets the index to use.
	/// </summary>
	/// <param name="index">Name of index</param>
	/// <exception cref="ArgumentException"><paramref name="index"/> was <see langword="null"/> or empty.</exception>
	public void SetIndex(string index)
	{
		if (string.IsNullOrEmpty(index)) throw new ArgumentException($"'{nameof(index)}' cannot be null or empty.", nameof(index));

		this.VerifyNotDisposed();
		Api.JetSetCurrentIndex(this.sesid, this.tableId, index);
	}

	/// <summary>
	/// Moves to the next record.
	/// </summary>
	/// <returns><see langword="true"/> if a record is found; otherwise, <see langword="false"/>.</returns>
	/// <remarks>
	/// This method updates <see cref="HasCurrentRecord"/>.
	/// </remarks>
	public bool MoveNext()
	{
		this.HasCurrentRecord = Api.TryMove(this.sesid, this.tableId, JET_Move.Next, MoveGrbit.None);
		return this.HasCurrentRecord;
	}

	private void EnsureRecord()
	{
		if (!this.HasCurrentRecord)
			throw new InvalidOperationException(Messages.JetCursor_NoCurrentRecord);
	}

	/// <summary>
	/// Gets the size, in bytes, of the data in a specified column.
	/// </summary>
	/// <param name="colid">Column ID</param>
	/// <returns>The size of the data, in bytes.</returns>
	/// <remarks>
	/// Use <see cref="JetDatabase.GetColumnId(string, string)"/> to get the ID of a named column.
	/// </remarks>
	public int? GetColumnSize(JET_COLUMNID colid, int tag)
	{
		this.EnsureRecord();

		try
		{
			var res = Api.JetRetrieveColumn(
				this.sesid,
				this.tableId,
				colid,
				null,
				0,
				out int actualLength,
				RetrieveColumnGrbit.None,
				new JET_RETINFO() { itagSequence = tag }
				);
			if (res == JET_wrn.ColumnNull)
				return null;

			return actualLength;
		}
		catch (EsentCallbackFailedException)
		{
			// Silently fail
			return null;
		}
	}

	/// <summary>
	/// Gets the number of values in a specified column.
	/// </summary>
	/// <param name="colid">Column ID</param>
	/// <returns>The number of values</returns>
	public int GetValueCount(JET_COLUMNID colid)
	{
		this.EnsureRecord();

		JET_RETRIEVECOLUMN ret = new JET_RETRIEVECOLUMN()
		{
			columnid = colid,
			itagSequence = 0
		};
		var retcols = new JET_RETRIEVECOLUMN[] {
			ret
		};

		try
		{
			var res = Api.JetRetrieveColumns(
				this.sesid,
				this.tableId,
				retcols,
				1);

			return ret.itagSequence;
		}
		catch (EsentCallbackFailedException)
		{
			// Silently fail
			return 0;
		}
	}

	private T? RetrieveValue<T>(JET_COLUMNID colid, int tag, int fixedSize, Func<byte[], T> converter)
	{
		this.EnsureRecord();

		// TODO: Use native Jet API without array
		var bytes = ArrayPool<byte>.Shared.Rent(4);
		try
		{
			var res = Api.JetRetrieveColumn(
				this.sesid,
				this.tableId,
				colid,
				bytes,
				bytes.Length,
				out int actualLength,
				RetrieveColumnGrbit.None,
				new JET_RETINFO() { itagSequence = tag }
				);
			if (res == JET_wrn.ColumnNull)
				return default(T?);

			if (actualLength != fixedSize)
				throw new Exception(Messages.JetCursor_UnexpectedValueSize);

			T? value = converter(bytes);
			return value;
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(bytes);
		}
	}

	private T[] RetrieveValues<T>(JET_COLUMNID colid, int fixedSize, Func<byte[], T> converter)
	{
		this.EnsureRecord();

		var count = this.GetValueCount(colid);
		if (count == 0)
			return Array.Empty<T>();

		T[] values = new T[count];
		for (int i = 0; i < count; i++)
		{
			values[i] = this.RetrieveValue(colid, i + 1, fixedSize, converter);
		}
		return values;
	}

	private T[] RetrieveNonnullValues<T>(JET_COLUMNID colid, int fixedSize, Func<byte[], T> converter)
	{
		this.EnsureRecord();

		var count = this.GetValueCount(colid);
		if (count == 0)
			return Array.Empty<T>();

		List<T> values = new List<T>(count);
		for (int i = 0; i < count; i++)
		{
			var value = this.RetrieveValue(colid, i + 1, fixedSize, converter);
			if (value != null)
			{
				values.Add(value);
			}
		}
		return values.ToArray();
	}

	/// <summary>
	/// Reads the value of a column as a 8-bit unsigned integer.
	/// </summary>
	/// <param name="colid">Column ID</param>
	/// <returns>The value of the column as an <see cref="byte"/>.</returns>
	/// <remarks>
	/// Use <see cref="JetDatabase.GetColumnId(string, string)"/> to get the ID of a named column.
	/// </remarks>
	public byte? ReadByte(JET_COLUMNID colid, int tag = 1)
	{
		return this.RetrieveValue<byte?>(colid, tag, 1, bytes => bytes[0]);
	}

	/// <summary>
	/// Reads the value of a column as a bit.
	/// </summary>
	/// <param name="colid">Column ID</param>
	/// <returns>The value of the column as an <see cref="bool"/>.</returns>
	/// <remarks>
	/// Use <see cref="JetDatabase.GetColumnId(string, string)"/> to get the ID of a named column.
	/// </remarks>
	public bool ReadBit(JET_COLUMNID colid, int tag = 1)
	{
		return this.RetrieveValue<byte?>(colid, tag, 1, bytes => bytes[0]) != 0;
	}

	/// <summary>
	/// Reads the value of a column as a 16-bit signed integer.
	/// </summary>
	/// <param name="colid">Column ID</param>
	/// <returns>The value of the column as an <see cref="Int16"/>.</returns>
	/// <remarks>
	/// Use <see cref="JetDatabase.GetColumnId(string, string)"/> to get the ID of a named column.
	/// </remarks>
	public short? ReadInt16(JET_COLUMNID colid, int tag = 1)
	{
		return this.RetrieveValue<short?>(colid, tag, 2, bytes => BitConverter.ToInt16(bytes));
	}

	/// <summary>
	/// Reads the value of a column as a 16-bit unsigned integer.
	/// </summary>
	/// <param name="colid">Column ID</param>
	/// <returns>The value of the column as an <see cref="UInt16"/>.</returns>
	/// <remarks>
	/// Use <see cref="JetDatabase.GetColumnId(string, string)"/> to get the ID of a named column.
	/// </remarks>
	public ushort? ReadUInt16(JET_COLUMNID colid, int tag = 1)
	{
		return this.RetrieveValue<ushort?>(colid, tag, 2, bytes => BitConverter.ToUInt16(bytes));
	}

	/// <summary>
	/// Reads the value of a column as a 32-bit signed integer.
	/// </summary>
	/// <param name="colid">Column ID</param>
	/// <returns>The value of the column as an <see cref="Int32"/>.</returns>
	/// <remarks>
	/// Use <see cref="JetDatabase.GetColumnId(string, string)"/> to get the ID of a named column.
	/// </remarks>
	public int? ReadInt32(JET_COLUMNID colid, int tag = 1)
	{
		return this.RetrieveValue<int?>(colid, tag, 4, bytes => BitConverter.ToInt32(bytes));
	}

	/// <summary>
	/// Reads the value of a column as a 32-bit unsigned integer.
	/// </summary>
	/// <param name="colid">Column ID</param>
	/// <returns>The value of the column as an <see cref="UInt32"/>.</returns>
	/// <remarks>
	/// Use <see cref="JetDatabase.GetColumnId(string, string)"/> to get the ID of a named column.
	/// </remarks>
	public uint? ReadUInt32(JET_COLUMNID colid, int tag = 1)
	{
		return this.RetrieveValue<uint?>(colid, tag, 4, bytes => BitConverter.ToUInt32(bytes));
	}

	/// <summary>
	/// Reads the values of a multi-valued column as a 32-bit integer.
	/// </summary>
	/// <param name="colid">Column ID</param>
	/// <returns>The values in the column as an <see cref="Int32"/>.</returns>
	public int?[] ReadMultiInt32(JET_COLUMNID colid)
	{
		return this.RetrieveValues<int?>(colid, 4, bytes => BitConverter.ToInt32(bytes));
	}

	/// <summary>
	/// Reads the values of a multi-valued column as a 32-bit integer.
	/// </summary>
	/// <param name="colid">Column ID</param>
	/// <returns>The values in the column as an <see cref="Int32"/>.</returns>
	public int[] ReadNonnullMultiInt32(JET_COLUMNID colid)
	{
		return this.RetrieveNonnullValues(colid, 4, bytes => BitConverter.ToInt32(bytes));
	}

	/// <summary>
	/// Reads the value of a column as a 64-bit integer.
	/// </summary>
	/// <param name="colid">Column ID</param>
	/// <returns>The value of the column as an <see cref="Int64"/>.</returns>
	/// <remarks>
	/// Use <see cref="JetDatabase.GetColumnId(string, string)"/> to get the ID of a named column.
	/// </remarks>
	public long? ReadInt64(JET_COLUMNID colid, int tag = 1)
	{
		return this.RetrieveValue<long?>(colid, tag, 8, bytes => BitConverter.ToInt64(bytes));
	}

	/// <summary>
	/// Reads the value of a column as a 32-bit floating point value.
	/// </summary>
	/// <param name="colid">Column ID</param>
	/// <returns>The value of the column as an <see cref="float"/>.</returns>
	/// <remarks>
	/// Use <see cref="JetDatabase.GetColumnId(string, string)"/> to get the ID of a named column.
	/// </remarks>
	public float? ReadSingle(JET_COLUMNID colid, int tag = 1)
	{
		return this.RetrieveValue<float?>(colid, tag, 4, bytes => BitConverter.ToSingle(bytes));
	}

	/// <summary>
	/// Reads the value of a column as a 64-bit floating point value.
	/// </summary>
	/// <param name="colid">Column ID</param>
	/// <returns>The value of the column as an <see cref="double"/>.</returns>
	/// <remarks>
	/// Use <see cref="JetDatabase.GetColumnId(string, string)"/> to get the ID of a named column.
	/// </remarks>
	public double? ReadDouble(JET_COLUMNID colid, int tag = 1)
	{
		return this.RetrieveValue<double?>(colid, tag, 8, bytes => BitConverter.ToDouble(bytes));
	}

	/// <summary>
	/// Reads the value of a column as a GUID.
	/// </summary>
	/// <param name="colid">Column ID</param>
	/// <returns>The value of the column as an <see cref="Guid"/>.</returns>
	/// <remarks>
	/// Use <see cref="JetDatabase.GetColumnId(string, string)"/> to get the ID of a named column.
	/// </remarks>
	public Guid? ReadGuid(JET_COLUMNID colid, int tag = 1)
	{
		var bytes = this.ReadBytes(colid, tag);
		Guid? guid = (bytes == null) ? null : new Guid(bytes);
		return guid;
	}

	/// <summary>
	/// Reads the value of a column as a date.
	/// </summary>
	/// <param name="colid">Column ID</param>
	/// <returns>The value of the column as an <see cref="DateTime"/>.</returns>
	/// <remarks>
	/// Use <see cref="JetDatabase.GetColumnId(string, string)"/> to get the ID of a named column.
	/// </remarks>
	public DateTime? ReadDateTime(JET_COLUMNID colid, int tag = 1)
	{
		var value = this.ReadDouble(colid, tag);
		return (value.HasValue) ? DateTime.FromOADate(value.Value) : null;
	}

	/// <summary>
	/// Reads the value of a column as a UTF-16 string.
	/// </summary>
	/// <param name="colid">Column ID</param>
	/// <returns>The value of the column as an <see cref="Int32"/>.</returns>
	/// <remarks>
	/// Use <see cref="JetDatabase.GetColumnId(string, string)"/> to get the ID of a named column.
	/// </remarks>
	public string? ReadUtf16String(JET_COLUMNID colid, int tag = 1)
	{
		this.EnsureRecord();
		// TODO: Use interop to avoid array

		var bytes = ReadBytes(colid, tag);
		if (bytes == null)
			return null;

		var cbText = bytes.Length;
		if (cbText > 2)
		{
			// Check for and remove null terminator
			if (
				bytes[^1] == 0
				&& bytes[^2] == 0
				)
				cbText -= 2;
		}
		var str = Encoding.Unicode.GetString(bytes, 0, cbText);
		return str;
	}

	/// <summary>
	/// Reads the data of a column as a byte array.
	/// </summary>
	/// <param name="colid">Column ID</param>
	/// <returns>The bytes contained in the column.</returns>
	/// <remarks>
	/// Use <see cref="JetDatabase.GetColumnId(string, string)"/> to get the ID of a named column.
	/// </remarks>
	public byte[]? ReadBytes(JET_COLUMNID colid, int tag)
	{
		this.EnsureRecord();

		var cbValue = this.GetColumnSize(colid, tag);
		if (!cbValue.HasValue)
			return null;

		byte[] bytes = new byte[cbValue.Value];

		try
		{
			Api.JetRetrieveColumn(
				this.sesid,
				this.tableId,
				colid,
				bytes,
				bytes.Length,
				out var actualLength,
				RetrieveColumnGrbit.None,
				null
				);
		}
		catch (EsentCallbackFailedException)
		{
			// Silently fail
		}
		return bytes;
	}

	#region Updates
	private bool _updatePrepared;
	private void EnsureUpdatePrepared()
	{
		this.EnsureRecord();

		if (!this._updatePrepared)
		{
			Api.JetPrepareUpdate(this.sesid, this.tableId, JET_prep.Replace);
			this._updatePrepared = true;
		}
	}
	public void ApplyUpdate()
	{
		if (this._updatePrepared)
		{
			Api.JetUpdate(this.sesid, this.tableId);
			this._updatePrepared = false;
		}
		else
			throw new InvalidOperationException("This cursor does not have a pending update to apply.");
	}
	public void CancelUpdate()
	{
		if (this._updatePrepared)
		{
			Api.JetPrepareUpdate(this.sesid, this.tableId, JET_prep.Replace);
			this._updatePrepared = false;
		}
	}
	#endregion

	public void SetNull(JET_COLUMNID colid)
	{
		this.EnsureUpdatePrepared();
		Api.JetSetColumn(this.sesid, this.tableId, colid, null, 0, SetColumnGrbit.None, null);
	}

	public void SetColumnBytes(JET_COLUMNID colid, int tag, byte[] bytes)
	{
		this.EnsureUpdatePrepared();
		if (bytes is null) throw new ArgumentNullException(nameof(bytes));
		Api.JetSetColumn(this.sesid, this.tableId, colid, null, 0, SetColumnGrbit.None, new JET_SETINFO() { itagSequence = tag });
	}

	public void SetInt32(JET_COLUMNID colid, int tag, int value)
	{
		this.SetColumnBytes(colid, tag, BitConverter.GetBytes(value));
	}

	public void SetInt64(JET_COLUMNID colid, int tag, long value)
	{
		this.SetColumnBytes(colid, tag, BitConverter.GetBytes(value));
	}

	public void SetUtf16String(JET_COLUMNID colid, int tag, string value)
	{
		if (value is null) throw new ArgumentNullException(nameof(value));
		this.SetColumnBytes(colid, tag, Encoding.Unicode.GetBytes(value));
	}
}

partial class JetCursor : IDisposable
{
	public bool IsDisposed { get; private set; }
	protected void VerifyNotDisposed()
	{
		if (this.IsDisposed)
			throw new ObjectDisposedException(Messages.JetObjectDisposedMessage);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!IsDisposed)
		{
			try
			{
				Api.JetCloseTable(this.sesid, this.tableId);
				this.HasCurrentRecord = false;
			}
			catch { }

			IsDisposed = true;
		}
	}

	~JetCursor()
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
