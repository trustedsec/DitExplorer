using DitExplorer.Ntds;
using DitExplorer.UI.WpfApp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.UI.WpfApp
{
	class ObjectSearchViewModel : ViewModel
	{
		public ObjectSearchViewModel(IDirectoryObject searchRoot, DirectoryView directory)
		{
			if (searchRoot == null) throw new ArgumentNullException(nameof(SearchRoot));
			SearchRoot = searchRoot;
			this.directory = directory;
			RegisterCommand(MyCommands.SearchNow, SearchNow);

			ListVM = new DirectoryListViewModel((IItemActionRegistry)App.services.GetService(typeof(IItemActionRegistry)), directory);

			Title = Messages.SubtreeSearch_Title + " - " + SearchRoot.DistinguishedName;

			var classes = directory.Directory.GetClassSchemas();
			Array.Sort(classes, (x, y) => x.LdapDisplayName.CompareTo(y.LdapDisplayName));
			List<IClassSchema?> classList = new List<IClassSchema?>(classes.Length + 1);
			classList.Add(null);
			classList.AddRange(classes);
			this.Classes = classList;
		}

		public string? Title { get; }

		public DirectoryListViewModel ListVM { get; private set; }
		public IList<IClassSchema?> Classes { get; }

		private IClassSchema? _selectedClass;
		public IClassSchema? SelectedClass
		{
			get { return _selectedClass; }
			set => this.NotifyIfChanged(ref _selectedClass, value);
		}

		private bool _includesSubclasses = true;
		public bool IncludesSubclasses
		{
			get { return _includesSubclasses; }
			set => this.NotifyIfChanged(ref _includesSubclasses, value);
		}


		public IDirectoryObject SearchRoot { get; }
		public string SearchRootPath => SearchRoot.DistinguishedName;


		private int _searchMode;

		public int SearchMode
		{
			get { return _searchMode; }
			set => NotifyIfChanged(ref _searchMode, value);
		}



		private string? _searchName;

		public string? SearchName
		{
			get { return _searchName; }
			set => NotifyIfChanged(ref _searchName, value);
		}


		private IList<IDirectoryNode> _results;
		private readonly DirectoryView directory;

		public IList<IDirectoryNode> Results
		{
			get { return _results; }
			set => NotifyIfChanged(ref _results, value);
		}

		private void SearchNow()
		{
			var results = this.SearchRoot.SearchSubtree(this.SearchName, this.SelectedClass, this.IncludesSubclasses);
			var array = results.Select(r => directory.NodeForObject(r)).ToArray();
			Results = array;
		}

	}
}
