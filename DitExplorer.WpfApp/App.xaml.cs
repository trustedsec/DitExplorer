using DitExplorer.UI.Behaviors;
using DitExplorer.UI.WpfApp.Services;
using System.ComponentModel;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Design;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Xml;

namespace DitExplorer.UI.WpfApp;
/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		this.InitMef();

		var services = App.services;
		var itemActions = new ItemActionRegistry(this._compContainer);
		_itemActions = itemActions;
		services.AddService(typeof(IItemActionRegistry), itemActions);
		//itemActions.RegisterAction(new Actions.ViewMembersAction());

		AppViewModel appVM = new AppViewModel();

		CommandFrame.AddGlobalCommandHandler(new GlobalCommandHandler());

		(this.MainWindow = new MainWindow()
		{
			DataContext = appVM
		}).Show();
	}

	#region Extensibility
	private CompositionContainer _compContainer;

	private void InitMef()
	{
		var dir = MethodInfo.GetCurrentMethod().Module.FullyQualifiedName;
		dir = Path.GetDirectoryName(dir);
		dir = Path.Combine(dir, "Extensions");

		string[]? manifestFileNames = null;
		try
		{
			manifestFileNames = Directory.GetFiles(dir, "*.ditextmanifest", new EnumerationOptions() { MaxRecursionDepth = 2, RecurseSubdirectories = true });
		}
		catch (Exception ex)
		{
			// TODO: Log extension discovery error
		}

		AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

		const string ManifestSchemaUri = "xsd://DitExplorer/DitExtensionManifest.xsd";
		List<AssemblyCatalog> assemblyCatalogs = new List<AssemblyCatalog>(manifestFileNames.Length);
		foreach (var manifestFileName in manifestFileNames)
		{
			try
			{
				string manifestXml = File.ReadAllText(manifestFileName);
				XmlDocument xmlDoc = new XmlDocument();
				xmlDoc.LoadXml(manifestXml);
				var nm = new XmlNamespaceManager(xmlDoc.NameTable);
				nm.AddNamespace("dex", ManifestSchemaUri);
				var assemblyFileName = xmlDoc.SelectSingleNode("/dex:manifest/@assemblyFileName", nm)?.Value;
				if (!string.IsNullOrEmpty(assemblyFileName))
				{
					var extensionCodebase = Path.Combine(Path.GetDirectoryName(manifestFileName), assemblyFileName);
					var asm = Assembly.LoadFrom(extensionCodebase);
					// TODO: Log assembly load failure
					var asmcat = new AssemblyCatalog(asm);
					assemblyCatalogs.Add(asmcat);
				}
			}
			catch (Exception ex)
			{
				// TODO: Log extension error
			}
		}

		var masterCat = new AggregateCatalog(assemblyCatalogs.ToArray());
		this._compContainer = new CompositionContainer(masterCat);
	}

	private Assembly? CurrentDomain_AssemblyResolve(object? sender, ResolveEventArgs args)
	{
		var codebase = args.RequestingAssembly?.ManifestModule?.FullyQualifiedName;
		var name = new AssemblyName(args.Name);
		if (codebase is not null)
		{
			string candidateFileName = Path.Combine(Path.GetDirectoryName(codebase), name.Name + ".dll");
			if (File.Exists(candidateFileName))
			{
				try
				{
					var candidateName = AssemblyName.GetAssemblyName(candidateFileName);
					if (candidateName.Name == name.Name && candidateName.Version == name.Version)
					{
						var asm = Assembly.LoadFrom(candidateFileName);
						return asm;
					}
				}
				catch (Exception ex)
				{
					// TODO: Log in extension log
				}
			}
		}
		return null;
	}
	#endregion

	internal static readonly ServiceContainer services = new ServiceContainer();
	private static ItemActionRegistry _itemActions;

	internal static object ShowObjectInspector(object param, Window? owner)
	{
		if (param is System.Collections.IList list)
		{
			// TODO: Support multiple items
			param = list[0];
		}
		DirectoryNode node = param as DirectoryNode;
		ObjectInspectorViewModel vm = new ObjectInspectorViewModel(node, _itemActions);
		ObjectInspectorWindow wnd = new ObjectInspectorWindow() { DataContext = vm, WindowStartupLocation = WindowStartupLocation.CenterOwner };
		if (owner != null)
			wnd.Owner = owner;
		wnd.Show();
		return param;
	}
}


record class CellSpec(object? item, PropertyDescriptor? property)
{
}

class GlobalCommandHandler : ViewModel
{
	internal GlobalCommandHandler()
	{
		RegisterCommand<CellSpec>(MyCommands.CopyValue, CopyValue);
		RegisterCommand<ListView>(MyCommands.CopySelection, CopyRows);
		RegisterCommand<object>(ApplicationCommands.Copy, Copy);
		RegisterCommand<object>(MyCommands.CopyDN, CopyDN);
		RegisterCommand<DirectoryNode>(MyCommands.SearchSubtree, SearchSubtree);
	}


	private void SearchSubtree(DirectoryNode node)
	{
		ObjectSearchViewModel vm = new ObjectSearchViewModel(node.Object, node.Owner);
		SearchWindow wnd = new SearchWindow()
		{
			DataContext = vm,
			Owner = this.Window
		};
		wnd.Show();
	}

	private void CopyValue(CellSpec cell)
	{
		var value = cell.property.GetValue(cell.item);
		if (value != null)
		{
			Clipboard.SetText(value.ToString());
		}
	}

	private void CopyDN(object obj)
	{
		if (obj is System.Collections.IList list)
		{
			StringBuilder sb = new StringBuilder();
			foreach (var item in list)
			{
				if (item is DirectoryNode node)
				{
					if (sb.Length > 0)
						sb.AppendLine();
					sb.Append(node.Object.DistinguishedName);
				}
			}
			Clipboard.SetText(sb.ToString());
		}
		else if (obj is DirectoryNode node)
		{
			var text = node.Object.DistinguishedName;
			if (text != null)
				Clipboard.SetText(text);
		}
	}

	private void Copy(object obj)
	{
		if (obj != null)
		{
			var text = obj.ToString();
			if (text != null)
				Clipboard.SetText(text);
		}
	}

	private static void CopyRows(ListView lvw)
	{
		StringWriter writer = new StringWriter();
		TsvBuilder tsvb = new TsvBuilder(writer);

		var items = lvw.SelectedItems;
		var grid = (GridView)lvw.View;
		Type? prevType = null;

		PropertyDescriptorCollection? props = null;
		foreach (var item in items)
		{
			if (item != null)
			{
				var itemType = item.GetType();
				if (itemType != prevType || props == null)
				{
					props = TypeDescriptor.GetProperties(item);
					itemType = prevType;
				}

				foreach (var col in grid.Columns)
				{
					var prop = FlexGrid.GetDisplayProperty(col);
					if (prop == null)
					{
						var displayBinding = col.DisplayMemberBinding;
						if (displayBinding is Binding binding && !string.IsNullOrEmpty(binding.Path?.Path))
							prop = props.Find(binding.Path.Path, false);
					}

					if (prop != null)
					{
						string? text = null;
						try
						{
							var value = prop.GetValue(item);
							text = value?.ToString();
						}
						catch
						{
							// TODO: Indicate error in output?
						}

						tsvb.WriteValue(text);
					}
				}


			}

			tsvb.EndRecord();
		}

		var tsv = writer.ToString();
		Clipboard.SetText(tsv);
	}
}