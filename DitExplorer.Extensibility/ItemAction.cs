using System.Windows.Input;

namespace DitExplorer
{
	/// <summary>
	/// Represents an action that may be performed on one or more objects.
	/// </summary>
	/// <remarks>
	/// If the action only applies to a single item at a time, derive from <see cref="SingleItemAction{T}"/>.
	/// </remarks>
	public abstract class ItemAction
	{
		/// <summary>
		/// Gets the text displayed in the menu.
		/// </summary>
		/// <remarks>
		/// The text should be localized for the current culture and may contain an underscore to denote the access key.
		/// </remarks>
		public abstract string MenuText { get; }
		/// <summary>
		/// Determines whether the action can be executed on a set of items.
		/// </summary>
		/// <param name="items">Items to test</param>
		/// <returns><see langword="true"/> if this action can execute on the provided items; otherwise, <see langword="false"/>.</returns>
		public abstract bool CanExecute(object[] items, IItemActionContext context);
		public abstract void Execute(object[] items, IItemActionContext context);
	}

	public abstract class SingleItemAction<T> : ItemAction
	{
		public sealed override bool CanExecute(object[] items, IItemActionContext context)
		{
			return (items is not null) && (items.Length == 1) && items[0] is T typed && CanExecute(typed, context);
		}

		public abstract bool CanExecute(T items, IItemActionContext context);

		public sealed override void Execute(object[] items, IItemActionContext context)
		{
			this.Execute((T)items[0], context);
		}

		public abstract void Execute(T item, IItemActionContext context);
	}
}
