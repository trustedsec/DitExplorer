using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.UI.WpfApp.Search
{
	[Export(typeof(IDirectorySearchProvider))]
	internal class AnrSearchProvider : IDirectorySearchProvider
	{
		public IDirectorySearchHandler GetSearchHandler(
			IDirectoryObject searchRoot,
			ViewTechnology viewTech
			)
			=> viewTech switch
			{
				ViewTechnology.Wpf => new AnrSearchViewModel(searchRoot),
				_ => throw new NotSupportedException()
			};
	}

	internal class AnrSearchViewModel : ViewModel, IDirectorySearchHandler
	{
		private readonly IDirectoryObject searchRoot;

		internal AnrSearchViewModel(IDirectoryObject searchRoot)
		{
			var classes = searchRoot.Directory.GetClassSchemas();
			Array.Sort(classes, (x, y) => x.LdapDisplayName.CompareTo(y.LdapDisplayName));
			List<IClassSchema?> classList = new List<IClassSchema?>(classes.Length + 1);
			classList.Add(null);
			classList.AddRange(classes);

			this.Classes = classList;
			this.searchRoot = searchRoot;
		}

		public string Caption => Messages.Search_ByNameAccessText;

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

		private string? _searchName;

		public string? SearchName
		{
			get { return _searchName; }
			set => NotifyIfChanged(ref _searchName, value);
		}

		public IEnumerable<IDirectoryObject> GetResults()
		{
			var results = this.searchRoot.SearchSubtree(this.SearchName, this.SelectedClass, this.IncludesSubclasses);
			return results;
		}

		public object? GetView()
		{
			return new AnrSearchPage() { DataContext = this };
		}
	}
}
