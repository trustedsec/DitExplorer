using DitExplorer.Ntds;
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
public partial class App : Application, ICompositionHost
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

		CommandFrame.AddGlobalCommandHandler(new GlobalCommandHandler(this));

		(this.MainWindow = new MainWindow()
		{
			DataContext = appVM
		}).Show();
	}

	#region Extensibility
	private CompositionContainer _compContainer;
	CompositionContainer ICompositionHost.Container => this._compContainer;

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
		assemblyCatalogs.Add(new AssemblyCatalog(Assembly.GetExecutingAssembly()));
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
	private static readonly AttributeFormat[] intFormats = new AttributeFormat[]
	{
		new AttributeFormat(Messages.Format_General, "G"),
		new AttributeFormat(Messages.Format_Decimal, "N0"),
		new AttributeFormat(Messages.Format_Binary, "0b{0:B}"),
		new AttributeFormat(Messages.Format_Hexadecimal, "0x{0:X}"),
	};
	private static readonly AttributeFormat[] enumFormats = new AttributeFormat[]
	{
		new AttributeFormat(Messages.Format_Flags, "G"),
		new AttributeFormat(Messages.Format_Decimal, "D"),
		new AttributeFormat(Messages.Format_Hexadecimal, "0x{0:X}"),
	};
	private static readonly AttributeFormat[] oidFormats = new AttributeFormat[]
	{
		new AttributeFormat("LDAP name", "L"),
		new AttributeFormat("Distinguished Name", "D"),
		new AttributeFormat("OID", "O"),
		new AttributeFormat("Encoded", "N"),
		new AttributeFormat("Encoded (hex)", "0x{0:X}"),
	};
	private static readonly AttributeFormat[] dateFormats = new AttributeFormat[]
	{
		new AttributeFormat(Messages.Format_General, "G"),
		new AttributeFormat(Messages.Format_TicksDecimal, "N"),
		new AttributeFormat(Messages.Format_TicksHex, "0x{0:X}"),
		new AttributeFormat(Messages.Format_ShortDate, "d"),
		new AttributeFormat(Messages.Format_LongDate, "D"),
		new AttributeFormat(Messages.Format_ShortTime, "t"),
		new AttributeFormat(Messages.Format_LongTime, "T"),
		new AttributeFormat(Messages.Format_ShortDateTime, "f"),
		new AttributeFormat(Messages.Format_LongDateTime, "F"),
		new AttributeFormat(Messages.Format_MonthDay, "m"),
		new AttributeFormat(Messages.Format_YearMonth, "y"),
		new AttributeFormat(Messages.Format_Roundtrip, "O"),
		new AttributeFormat(Messages.Format_Rfc1123, "R"),
		new AttributeFormat(Messages.Format_Sortable, "s"),
	};
	internal static AttributeFormat[] GetDisplayFormatsFor(PropertyDescriptor prop)
	{
		if (
			(prop is DirectoryPropertyDescriptor dirprop)
			&& (dirprop.AttributeSchema.Syntax?.IsFormattable ?? false)
			)
		{
			return dirprop.AttributeSchema.Syntax.GetDisplayFormats();
		}
		else if (prop.PropertyType.IsEnum)
		{
			return enumFormats;
		}
		else if (prop.PropertyType == typeof(DirectoryTime))
		{
			return dateFormats;
		}
		else if (prop.PropertyType == typeof(DirectoryOid))
		{
			return oidFormats;
		}
		else
		{
			var tc = Type.GetTypeCode(prop.PropertyType);
			return tc switch
			{
				TypeCode.Byte
				or TypeCode.SByte
				or TypeCode.Int16
				or TypeCode.UInt16
				or TypeCode.Int32
				or TypeCode.UInt32
				or TypeCode.Int64
				or TypeCode.UInt64 => intFormats,
				_ => Array.Empty<AttributeFormat>()
			};
		}
	}
}

internal interface ICompositionHost
{
	CompositionContainer Container { get; }
}

record class CellSpec(object? item, PropertyDescriptor? property)
{
}

class GlobalCommandHandler : ViewModel
{
	private readonly ICompositionHost host;

	internal GlobalCommandHandler(ICompositionHost host)
	{
		this.host = host;

		RegisterCommand<object>(MyCommands.CopyValue, CopyValue);
		RegisterCommand<ListView>(MyCommands.CopySelection, CopyRows);
		RegisterCommand<object>(ApplicationCommands.Copy, Copy);
		RegisterCommand<object>(MyCommands.CopyDN, CopyDN);
		RegisterCommand<DirectoryNode>(MyCommands.SearchSubtree, SearchSubtree);
		RegisterCommand<object>(MyCommands.ShowAdSchemaDocHelp, ShowAdSchemaDoc, null, includeNull: true);
	}

	private void ShowAdSchemaDoc(object obj)
	{
		if (obj is IDirectoryNode node)
			Actions.ShowSchemaHelpAction.ShowDocFor(node);
		else
			Actions.ShowSchemaHelpAction.ShowDocFor(obj as IDirectoryObject);
	}


	private void SearchSubtree(DirectoryNode node)
	{
		ObjectSearchViewModel vm = new ObjectSearchViewModel(this.host, node.Object, node.Owner);
		SearchWindow wnd = new SearchWindow()
		{
			DataContext = vm,
			Owner = this.Window
		};
		wnd.Show();
	}

	private void CopyValue(object target)
	{
		if (target is CellSpec cell)
		{
			var value = cell.property.GetValue(cell.item);
			if (value != null)
				Clipboard.SetText(value.ToString());
		}
		else if (target is TextBlock textBlock)
		{
			var value = textBlock.Text;
			if (value != null)
				Clipboard.SetText(value);
		}
		else
		{
			while (target != null)
			{
				if (target is ContentPresenter pres)
					target = pres.Content;
				else if (target is Decorator dec)
					target = dec.Child;
				else if (target is TextBlock txt)
					target = txt.Text;
			}

			if (target is string str)
				Clipboard.SetText(str);
			else
			{
				var value = target?.ToString();
				if (value != null)
					Clipboard.SetText(value);
			}
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