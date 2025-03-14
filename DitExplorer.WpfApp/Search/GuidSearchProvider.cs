using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.UI.WpfApp.Search
{
	[Export(typeof(IDirectorySearchProvider))]
	internal class GuidSearchProvider : IDirectorySearchProvider
	{
		public IDirectorySearchHandler GetSearchHandler(IDirectoryObject searchRoot, ViewTechnology viewTech)
			=> viewTech switch
			{
				ViewTechnology.Wpf => new GuidSearchHandler(searchRoot),
				_ => throw new NotSupportedException()
			};
	}

	internal class GuidSearchHandler : ViewModel, IDirectorySearchHandler
	{
		private IDirectoryObject searchRoot;

		public GuidSearchHandler(IDirectoryObject searchRoot)
		{
			this.searchRoot = searchRoot;
		}

		private Guid? _searchGuid;
		private string? _searchGuidText;
		public string? SearchGuid
		{
			get { return _searchGuidText; }
			set
			{
				if (this.NotifyIfChanged(ref this._searchGuidText, value))
				{
					try
					{
						this._searchGuid = Guid.Parse(value);
					}
					catch
					{
						this._searchGuid = null;
						throw;
					}
				}
			}
		}


		public string Caption => Messages.Search_ByGuidCaption;

		public IEnumerable<IDirectoryObject> GetResults()
		{
			if (this._searchGuid.HasValue)
				return new IDirectoryObject[]{
					searchRoot.Directory.GetObjectByGuid(this._searchGuid.Value)
				};
			else
				throw new Exception("No valid GUID entered.  Enter a valid GUID and try again.");
		}

		public object? GetView()
			=> new GuidSearchPage() { DataContext = this };
	}
}
