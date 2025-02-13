using DitExplorer.Ntds;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.UI.WpfApp
{
	class DirectoryView : IDirectoryView
	{
		internal DirectoryView(IDirectory directory)
		{
			if (directory is null) throw new ArgumentNullException(nameof(directory));
			Directory = directory;

			// Build property descriptors
			var attrs = directory.GetAllAttributeSchemas();

			var classProps = TypeDescriptor.GetProperties(typeof(DirectoryNode));
			List<PropertyDescriptor> props = new List<PropertyDescriptor>();
			foreach (PropertyDescriptor prop in classProps)
				props.Add(prop);
			foreach (var attr in attrs)
			{
				var prop = new DirectoryPropertyDescriptor(attr);
				props.Add(prop);
			}
			attrPropList = props;
			attrProps = new PropertyDescriptorCollection(props.ToArray(), true);
		}

		public IDirectory Directory { get; }

		internal IList<PropertyDescriptor> attrPropList;
		internal PropertyDescriptorCollection attrProps;

		internal DirectoryNode NodeForObject(IDirectoryObject obj)
		{
			if (obj is null) throw new ArgumentNullException(nameof(obj));
			return new DirectoryNode(obj, this);
		}
	}
}
