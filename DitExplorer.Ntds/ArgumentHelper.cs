using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.Ntds;
static class ArgumentHelper
{
	public static T ThrowIfNot<T>(object? value, [CallerArgumentExpression(nameof(value))] string? expression = null)
		where T : class
		=> (value is T typed) ? typed : throw new ArgumentException("The argument is of the wrong type.", expression);
	public static T? ThrowIfNotNullAndNot<T>(object? value, [CallerArgumentExpression(nameof(value))] string? expression = null)
		where T : class
		=> (value is null) ? null
		: (value is T typed) ? typed : throw new ArgumentException("The argument is of the wrong type.", expression);
	public static IList<T> ThrowIfNot<T>(System.Collections.IList value, [CallerArgumentExpression(nameof(value))] string? expression = null)
		where T : class
	{
		if (!value.OfType<object>().All(r => r is T))
			throw new ArgumentException("Not all values in the list are of the required type.", expression);

		return value.OfType<T>().ToArray();
	}
	public static IList<TRequired> ThrowIfNot<TSource, TRequired>(this IList<TSource> value, [CallerArgumentExpression(nameof(value))] string? expression = null)
		where TSource : class
		where TRequired : TSource
	{
		if (!value.All(r => r is TRequired))
			throw new ArgumentException("Not all values in the list are of the required type.", expression);

		return value.Cast<TRequired>().ToArray();
	}
}
