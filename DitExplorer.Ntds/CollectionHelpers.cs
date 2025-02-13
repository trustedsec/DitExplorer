using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.Ntds;
internal static class CollectionHelpers
{
	public static void AddRange<T>(this HashSet<T> hashset, IEnumerable<T> values)
	{
		foreach (var item in values)
		{
			hashset.Add(item);
		}
	}
}
