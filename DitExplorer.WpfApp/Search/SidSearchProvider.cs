using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.UI.WpfApp.Search
{
	[Export(typeof(IDirectorySearchProvider))]
	internal class SdSearchProvider : IDirectorySearchProvider
	{
		public IDirectorySearchHandler GetSearchHandler(IDirectoryObject searchRoot, ViewTechnology viewTech)
			=> viewTech switch
			{
				ViewTechnology.Wpf => new SidSearchHandler(searchRoot),
				_ => throw new NotSupportedException()
			};
	}

	internal class SidSearchHandler : ViewModel, IDirectorySearchHandler
	{
		private IDirectoryObject searchRoot;

		public SidSearchHandler(IDirectoryObject searchRoot)
		{
			this.searchRoot = searchRoot;
		}

		private SecurityIdentifier? _searchSid;
		private string? _searchSidText;
		public string? SearchSid
		{
			get { return _searchSidText; }
			set
			{
				if (this.NotifyIfChanged(ref this._searchSidText, value))
				{
					try
					{
						this._searchSid = new SecurityIdentifier(value);
					}
					catch
					{
						this._searchSid = null;
						throw;
					}
				}
			}
		}


		public string Caption => Messages.Search_BySidCaption;

		public IEnumerable<IDirectoryObject> GetResults()
		{
			if (this._searchSid != null)
				return new IDirectoryObject[]{
					searchRoot.Directory.GetObjectBySid(this._searchSid)
				};
			else
				throw new Exception("No valid SID entered.  Enter a valid SID and try again.");
		}

		public object? GetView()
			=> new SidSearchPage() { DataContext = this };
	}
}
