using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer;

/// <summary>
/// Represents the value of an attribute that can hold multiple values.
/// </summary>
public abstract class MultiValue : System.Collections.IEnumerable
{
	/// <summary>
	/// Gets the element type;
	/// </summary>
	public abstract Type ElementType { get; }

	/// <summary>
	/// Gets the array holding the individual values.
	/// </summary>
	protected abstract Array ValueArray { get; }

	/// <summary>
	/// Gets the number of elements.
	/// </summary>
	public abstract int Count { get; }

	/// <summary>
	/// Gets an element at a 0-based index.
	/// </summary>
	/// <param name="index">0-based index</param>
	/// <returns>Value at <paramref name="index"/></returns>
	public abstract object? GetElementAt(int index);

	/// <summary>
	/// Instantiates a <see cref="MultiValue{T}"/> from the values in an array.
	/// </summary>
	/// <param name="array">Array holding values</param>
	/// <returns>A <see cref="MultiValue{T}"/> holding the values of <paramref name="array"/></returns>
	/// <exception cref="ArgumentNullException"><paramref name="array"/> is <see langword="null"/></exception>
	public static MultiValue Create(Array array)
	{
		if (array is null) throw new ArgumentNullException(nameof(array));

		var elemType = array.GetType().GetElementType();
		var multiType = typeof(MultiValue<>).MakeGenericType(elemType);
		var multi = (MultiValue)Activator.CreateInstance(multiType, array, array.Length)!;
		return multi;
	}

	/// <summary>
	/// Instantiates a <see cref="MultiValue{T}"/> from the values in an array.
	/// </summary>
	/// <param name="array">Array holding values</param>
	/// <param name="count">Number of elements in <paramref name="array"/> to use</param>
	/// <returns>A <see cref="MultiValue{T}"/> holding the values of <paramref name="array"/></returns>
	/// <exception cref="ArgumentNullException"><paramref name="array"/> is <see langword="null"/></exception>
	public static MultiValue Create(Array array, int count)
	{
		if (array is null) throw new ArgumentNullException(nameof(array));

		var elemType = array.GetType().GetElementType();
		var multiType = typeof(MultiValue<>).MakeGenericType(elemType);
		var multi = (MultiValue)Activator.CreateInstance(multiType, array, count)!;
		return multi;
	}

	/// <summary>
	/// Instantiates a <see cref="MultiValue{T}"/> from the values in an array.
	/// </summary>
	/// <typeparam name="T">Type of element</typeparam>
	/// <param name="elements">Array holding values</param>
	/// <returns>A <see cref="MultiValue{T}"/> holding the values of <paramref name="elements"/></returns>
	/// <exception cref="ArgumentNullException"><paramref name="elements"/> is <see langword="null"/></exception>
	public static MultiValue<T> Create<T>(T[] elements)
	{
		if (elements is null) throw new ArgumentNullException(nameof(elements));

		return new MultiValue<T>(elements, elements.Length);
	}

	/// <summary>
	/// Gets the element type for a multivalue type.
	/// </summary>
	/// <param name="multivalueType">A closed multivalue <see cref="Type"/>.</param>
	/// <returns>Element type of <paramref name="multivalueType"/>, if it is <see cref="MultiValue{T}"/>; otherwise, <see langword="null"/></returns>
	/// <exception cref="ArgumentNullException"><paramref name="multivalueType"/> is <see langword="null"/></exception>
	/// <remarks>
	/// If <paramref name="multivalueType"/> is not a <see cref="MultiValue{T}"/>, this is
	/// indicated by a return value of <see langword="null"/> and does not throw an exception.
	/// </remarks>
	public static Type? GetElementType(Type multivalueType)
	{
		if (multivalueType is null) throw new ArgumentNullException(nameof(multivalueType));

		if (multivalueType.IsGenericType && !multivalueType.IsGenericTypeDefinition)
		{
			var def = multivalueType.GetGenericTypeDefinition();
			if (def == typeof(MultiValue<>))
			{
				return def.GetGenericArguments()[0];
			}
		}
		return null;
	}

	/// <inheritdoc/>
	public IEnumerator GetEnumerator() => this.ValueArray.GetEnumerator();
}

/// <summary>
/// Represents the value of an attribute that can hold multiple values.
/// </summary>
/// <typeparam name="T">The type of the attribute </typeparam>
public sealed partial class MultiValue<T> : MultiValue, IEnumerable<T>
{
	/// <summary>
	/// Initializes a new <see cref="MultiValue{T}"/>.
	/// </summary>
	/// <param name="values">Values contained in the attribute</param>
	/// <exception cref="ArgumentNullException"><paramref name="values"/> is <see langword="null"/></exception>
	public MultiValue(T[] values, int count)
	{
		if (values is null) throw new ArgumentNullException(nameof(values));
		if (count > values.Length) throw new ArgumentOutOfRangeException(nameof(count));

		if (count < values.Length)
			Array.Resize(ref values, count);

		this.Values = values;
	}
	/// <summary>
	/// Gets the individual values.
	/// </summary>
	public T[] Values { get; }

	/// <inheritdoc/>
	protected sealed override Array ValueArray => this.Values;

	/// <inheritdoc/>
	public sealed override Type ElementType => throw new NotImplementedException();

	/// <inheritdoc/>
	public sealed override int Count => this.Values.Length;
	/// <inheritdoc/>
	public sealed override object? GetElementAt(int index)
		=> this.Values[index];

	private string? _cachedString;

	/// <inheritdoc/>
	public sealed override string ToString()
		=> (this._cachedString ?? this.BuildString());
	private string BuildString()
	{
		StringBuilder sb = new StringBuilder();
		foreach (var item in this.Values)
		{
			if (item != null)
			{
				var str = item.ToString();
				if (sb.Length > 0)
					sb.Append("; ");
				sb.Append(str);
			}
		}
		return sb.ToString();
	}

	IEnumerator<T> IEnumerable<T>.GetEnumerator()
	{
		return ((IEnumerable<T>)Values).GetEnumerator();
	}
}

partial class MultiValue<T> : IComparable<MultiValue<T>>, IComparable
{
	/// <inheritdoc/>
	public int CompareTo(MultiValue<T>? other)
	{
		if (other is null)
			return -1;

		var count = Math.Min(this.Values.Length, other.Values.Length);
		for (int i = 0; i < count; i++)
		{
			var x = this.Values[i];
			var y = this.Values[i];

			int cmp = Comparer<T>.Default.Compare(x, y);
			if (cmp != 0)
				return cmp;
		}

		{
			int cmp = this.Values.Length - other.Values.Length;
			return cmp;
		}
	}

	/// <inheritdoc/>
	public int CompareTo(object? obj)
	{
		if (obj is MultiValue<T> other)
			return this.CompareTo(other);
		else
			throw new ArgumentException("Other object is not of the same type.", nameof(other));
	}

}
