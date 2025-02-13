using DitExplorer.Ntds;
using DitExplorer.WpfApp;
using System.ComponentModel.Composition;
using System.Windows;

namespace DitExplorer.CredentialExtraction
{
	[Export(typeof(ItemAction))]
	internal sealed class ExtractCredentialsAction : ItemAction
	{
		[ImportingConstructor]
		internal ExtractCredentialsAction()
		{

		}

		public override string MenuText => "E_xtract Credentials...";

		public sealed override bool CanExecute(object[] items, IItemActionContext context)
		{
			bool found = false;
			foreach (var item in items)
				if (IsEligible(item))
				{
					found = true;
					break;
				}
			return found;
		}

		private static bool IsEligible(object item)
		{
			if (item is IDirectoryNode node)
			{
				var objcls = node.Object.ObjectClass;
				bool hasCredentials =
					objcls.HasAttribute("unicodePwd")
					|| objcls.HasAttribute("dBCSPwd")
					|| objcls.HasAttribute("lmPwdHistory")
					|| objcls.HasAttribute("ntPwdHistory")
					|| objcls.HasAttribute("supplementalCredentials");
				if (hasCredentials)
					return true;
			}

			return false;
		}

		public sealed override void Execute(object[] items, IItemActionContext context)
		{
			List<IDirectoryNode> nodes = new List<IDirectoryNode>(items.Length);
			foreach (var item in items)
				if (IsEligible(item))
					nodes.Add((IDirectoryNode)item);

			if (nodes.Count > 0)
			{
				CredentialExtractorViewModel vm = new CredentialExtractorViewModel(nodes.ToArray());
				CredentialExtractorWindow wnd = new CredentialExtractorWindow()
				{
					DataContext = vm,
					Owner = context.Owner,
					WindowStartupLocation = WindowStartupLocation.CenterOwner
				};
				wnd.Show();
			}
		}
	}
}
