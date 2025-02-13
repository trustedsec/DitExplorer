namespace DitExplorer;

public interface IItemActionRegistry
{
	IList<ItemAction> GetActionsFor(
		object[] items,
		IItemActionContext actionContext
		);
	void RegisterAction(ItemAction action);
}