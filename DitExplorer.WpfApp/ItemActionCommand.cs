using System.Windows.Input;

namespace DitExplorer.UI.WpfApp;
internal class ItemActionCommand : ICommand
{
	private readonly ItemAction _action;
	private readonly IItemActionContext _context;

	public ItemActionCommand(ItemAction action, IItemActionContext context)
	{
		if (action is null) throw new ArgumentNullException(nameof(action));
		this._action = action;
		this._context = context;
	}

	public event EventHandler? CanExecuteChanged;

	public bool CanExecute(object? parameter)
	{
		return _action.CanExecute(parameter as object[], this._context);
	}

	public void Execute(object? parameter)
	{
		_action.Execute((object[])parameter, this._context);
	}
}