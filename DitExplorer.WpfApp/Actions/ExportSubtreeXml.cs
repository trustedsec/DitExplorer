using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace DitExplorer.UI.WpfApp.Actions
{
	[Export(typeof(ItemAction))]
	internal class ExportSubtreeXml : SingleItemAction<IDirectoryNode>
	{
		public override string MenuText => Messages.Action_ExportSubtreeXmlMenuText;

		public override bool CanExecute(IDirectoryNode item, IItemActionContext context)
			=> true;

		private const string SubtreeXmlSchemaName = "xsd://ditexplorer/subtree.xsd";
		public override void Execute(IDirectoryNode item, IItemActionContext context)
		{
			SaveFileDialog fd = new SaveFileDialog
			{
				Filter = "XML (*.xml)|*.xml",
				FileName = item.Object.Name + ".xml",
				Title = Messages.Export_SubtreeXmlDialogTitle,
			};

			var res = fd.ShowDialog(context.Owner);
			if (res ?? false)
			{
				ExportContext exportContext = new ExportContext
				{
					attributes = item.DirectoryView.Directory.GetAllAttributeSchemas()
				};

				string fileName = fd.FileName;
				using (var writer = XmlWriter.Create(fileName))
				{
					writer.WriteStartDocument();
					writer.WriteStartElement("subtree", SubtreeXmlSchemaName);

					writer.WriteAttributeString("dn", item.Object.DistinguishedName);

					WriteNode(item.Object, writer, ref exportContext);

					writer.WriteEndElement();
				}

				UiHelper.ReportExportSuccess(fd.FileName, exportContext.count, Messages.Export_SubtreeXmlDialogTitle, context.Owner);
			}
		}

		struct ExportContext
		{
			internal IList<IAttributeSchema> attributes;
			internal int count;
		}

		private void WriteNode(IDirectoryObject obj, XmlWriter writer, ref ExportContext context)
		{
			context.count++;

			writer.WriteStartElement("node");

			writer.WriteAttributeString("name", obj.Name);
			if (obj.ObjectClass != null)
				writer.WriteAttributeString("objClass", obj.ObjectClass.Name);

			var attrs = context.attributes;
			var values = obj.GetValueOfMultiple(attrs);
			for (int i = 0; i < attrs.Count; i++)
			{
				var value = values[i];
				var attr = attrs[i];
				if (value is MultiValue multi)
				{
					foreach (var item in multi)
					{
						WriteAttrValue(item, attr, writer);
					}
				}
				else if (value is not null)
				{
					WriteAttrValue(value, attr, writer);
				}
			}

			var children = obj.GetChildren();
			foreach (var child in children)
			{
				this.WriteNode(child, writer, ref context);
			}

			writer.WriteEndElement();
		}

		private void WriteAttrValue(object value, IAttributeSchema attr, XmlWriter writer)
		{
			Debug.Assert(value != null);

			writer.WriteStartElement("node", SubtreeXmlSchemaName);
			writer.WriteAttributeString("name", attr.LdapDisplayName);

			if (attr.Syntax != null)
				writer.WriteAttributeString("syntax", attr.Syntax.LdapName);

			if (value is byte[] byteArray)
				writer.WriteValue(byteArray.ToHexString());
			else
				writer.WriteValue(value.ToString());

			writer.WriteEndElement();
		}
	}
}
