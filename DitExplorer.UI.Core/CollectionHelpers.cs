using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace DitExplorer.UI;
public static class CollectionHelpers
{
	public static void DispatchAdd<T, TList>(this TList list, T itemToAdd, Dispatcher dispatcher, DispatcherPriority priority)
		where TList : IList<T>
	{
		if (dispatcher is null) throw new ArgumentNullException(nameof(dispatcher));

		dispatcher.BeginInvoke((TList list, T item) => { list.Add(item); }, priority, list, itemToAdd);
	}
	public static void DispatchAddRange<T, TList>(this TList list, IEnumerable<T> enumerable, Dispatcher dispatcher, DispatcherPriority priority)
		where TList : IList<T>
	{
		if (enumerable is null) throw new ArgumentNullException(nameof(enumerable));
		if (dispatcher is null) throw new ArgumentNullException(nameof(dispatcher));

		foreach (var item in enumerable)
			list.DispatchAdd(item, dispatcher, priority);
	}

	public static object[] ToArray(this System.Collections.IList list)
	{
		object[] arr = new object[list.Count];
		for (int i = 0; i < arr.Length; i++)
			arr[i] = list[i];
		return arr;
	}
}
