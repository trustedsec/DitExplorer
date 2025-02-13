using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.UI.WpfApp.Services;
internal class ItemActionRegistry : IItemActionRegistry
{
	internal ItemActionRegistry(CompositionContainer container)
	{
		this._container = container;
	}

	private List<ItemAction> _actions = new List<ItemAction>();
	private readonly CompositionContainer _container;

	public void RegisterAction(ItemAction action)
	{
		if (action is null) throw new ArgumentNullException(nameof(action));
		_actions.Add(action);
	}
	public IList<ItemAction> GetActionsFor(object[] items, IItemActionContext actionContext)
	{
		if (items is null || items.Length == 0 || Array.IndexOf(items, null) >= 0) throw new ArgumentNullException(nameof(items));


		List<ItemAction> eligibleActions = new List<ItemAction>(items.Length);
		var extensionActions = this._container.GetExports<ItemAction, IItemActionMetadata>();
		foreach (var action in extensionActions)
		{
			try
			{
				if (action.Value.CanExecute(items, actionContext))
					eligibleActions.Add(action.Value);
			}
			catch
			{
				// TODO: Log extension error
				// Silently fail
			}
		}

		foreach (var action in _actions)
		{
			try
			{
				if (action.CanExecute(items, actionContext))
					eligibleActions.Add(action);
			}
			catch
			{
				// Silently fail
			}
		}

		return eligibleActions;
	}
}
