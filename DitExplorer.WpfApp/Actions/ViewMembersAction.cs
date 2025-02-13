using DitExplorer.Ntds;

namespace DitExplorer.UI.WpfApp.Actions
{
	public sealed class ViewMembersAction : SingleItemAction<IDirectoryNode>
	{
		public override string MenuText => "View Members";

		public sealed override bool CanExecute(IDirectoryNode node, IItemActionContext context)
			=> node.Object.ObjectClass.HasAttribute("member")
			|| node.Object.ObjectClass.HasAttribute("memberOf");

		public sealed override void Execute(IDirectoryNode node, IItemActionContext context)
		{
		}
	}
}
