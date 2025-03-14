using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer;

/// <summary>
/// Provides useful functionality for lists.
/// </summary>
public static class ListExtensions
{
	public static IEnumerable<TResult> FullOuterHashJoin<TLeft, TRight, TKey, TResult>(
		this IEnumerable<TLeft> left,
		IEnumerable<TRight> right,
		Func<TLeft, TKey> leftKeySelector,
		Func<TRight, TKey> rightKeySelector,
		Func<TLeft, TRight, TResult> resultSelector
		)
	{
		var rightLookup = right.ToDictionary(rightKeySelector);

		foreach (var leftItem in left)
		{
			var key = leftKeySelector(leftItem);
			if (rightLookup.TryGetValue(key, out var rightItem))
				rightLookup.Remove(key);

			yield return resultSelector(leftItem, rightItem);
		}
		foreach (var item in rightLookup.Values)
		{
			yield return resultSelector(default, item);
		}
	}
}
