using DitExplorer.Ntds;
using DitExplorer.UI.WpfApp;
using DitExplorer.UI.WpfApp.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.UI.WpfApp
{
	class ObjectSearchViewModel : ViewModel
	{
		public ObjectSearchViewModel(
			ICompositionHost host,
			IDirectoryObject searchRoot,
			DirectoryView directory)
		{
			if (searchRoot == null) throw new ArgumentNullException(nameof(SearchRoot));
			SearchRoot = searchRoot;
			this.directory = directory;
			RegisterCommand(MyCommands.SearchNow, SearchNow);

			ListVM = new DirectoryListViewModel((IItemActionRegistry)App.services.GetService(typeof(IItemActionRegistry)), directory);

			Title = Messages.SubtreeSearch_Title + " - " + SearchRoot.DistinguishedName;

			List<SearchHandlerItem> handlers = new List<SearchHandlerItem>();
			var searchProviders = host.Container.GetExports<IDirectorySearchProvider>();
			foreach (var providerInfo in searchProviders)
			{
				try
				{
					var provider = providerInfo.Value;
					var handler = provider.GetSearchHandler(SearchRoot, ViewTechnology.Wpf);
					var header = handler.Caption;
					var content = handler.GetView();

					handlers.Add(new SearchHandlerItem
					{
						SearchHandler = handler,
						Header = header,
						Content = content
					});
				}
				catch
				{
					// Silently fail
				}
			}

			this._searchHandlers = handlers.ToArray();
			this.SelectedSearchHandler = handlers.FirstOrDefault();
		}

		public string? Title { get; }

		public DirectoryListViewModel ListVM { get; private set; }


		public IDirectoryObject SearchRoot { get; }
		public string SearchRootPath => SearchRoot.DistinguishedName;

		private IList<IDirectoryNode> _results;
		private readonly DirectoryView directory;

		private SearchHandlerItem[] _searchHandlers;
		public IReadOnlyList<SearchHandlerItem> SearchHandlers => this._searchHandlers;

		private SearchHandlerItem _selectedSearchHandler;

		public SearchHandlerItem SelectedSearchHandler
		{
			get { return _selectedSearchHandler; }
			set => this.NotifyIfChanged(ref _selectedSearchHandler, value);
		}



		public IList<IDirectoryNode> Results
		{
			get { return _results; }
			set => NotifyIfChanged(ref _results, value);
		}


		private void SearchNow()
		{
			if (this.SelectedSearchHandler != null)
			{
				var results = this.SelectedSearchHandler.SearchHandler.GetResults();
				var array = results.Select(r => directory.NodeForObject(r)).ToArray();
				Results = array;
			}
		}
	}

	class SearchHandlerItem
	{
		public string Header { get; set; }
		public object? Content { get; set; }
		public IDirectorySearchHandler SearchHandler { get; set; }
	}
}
