using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.UI.WpfApp.Search
{
	// UNDONE: Temporarily for public beta
	//[Export(typeof(IDirectorySearchProvider))]
	internal class LdapQuerySearchProvider : IDirectorySearchProvider
	{
		public IDirectorySearchHandler GetSearchHandler(IDirectoryObject searchRoot, ViewTechnology viewTech)
			=> viewTech switch
			{
				ViewTechnology.Wpf => new LdapQuerySearchHandler(searchRoot),
				_ => throw new NotSupportedException()
			};
	}

	internal class LdapQuerySearchHandler : ViewModel, IDirectorySearchHandler
	{
		private IDirectoryObject searchRoot;

		public LdapQuerySearchHandler(IDirectoryObject searchRoot)
		{
			this.searchRoot = searchRoot;
		}

		private string? _ldapQueryText;
		public string? LdapQueryText
		{
			get { return _ldapQueryText; }
			set
			{
				if (this.NotifyIfChanged(ref this._ldapQueryText, value))
				{
				}
			}
		}


		public string Caption => Messages.Search_ByLdapQuery;

		public IEnumerable<IDirectoryObject> GetResults()
		{
			throw new NotImplementedException();
		}

		public object? GetView()
			=> new SidSearchPage() { DataContext = this };
	}
}
