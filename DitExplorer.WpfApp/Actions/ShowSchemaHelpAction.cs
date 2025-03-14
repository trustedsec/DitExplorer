using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.UI.WpfApp.Actions;

[Export(typeof(ItemAction))]
internal class ShowSchemaHelpAction : SingleItemAction<IDirectoryNode>
{
	public override string MenuText => Messages.Help_AdSchemaDocMenuText;

	public override bool CanExecute(IDirectoryNode item, IItemActionContext context)
	{
		return item.Object?.ObjectClass != null;
	}

	private static bool IsSchemaObject(IDirectoryObject obj)
	{
		var schemaObj = obj as ISchemaObject;
		if (schemaObj is null)
			schemaObj = obj.ObjectClass;

		var cls = schemaObj.ObjectClass;
		bool isSchemaClass = IsSchemaClass(cls);
		return isSchemaClass;
	}

	private static bool IsSchemaClass(IClassSchema cls)
	{
		return
			cls.LdapDisplayName == SpecialClassNames.AttributeSchema
			|| cls.LdapDisplayName == SpecialClassNames.ClassSchema;
	}

	public override void Execute(IDirectoryNode item, IItemActionContext context)
	{
		ShowDocFor(item);
	}

	public static void ShowDocFor(IDirectoryNode item)
	{
		var obj = item?.Object;
		ShowDocFor(obj);
	}

	public const string MainUrl = "https://learn.microsoft.com/en-us/windows/win32/adschema/active-directory-schema";

	public static void ShowDocFor(IDirectoryObject? obj)
	{
		var schemaObj = obj as ISchemaObject;
		if (schemaObj is null)
			schemaObj = obj?.ObjectClass;

		string url = MainUrl;
		var cultureCode = CultureInfo.CurrentUICulture.Name;
		if (schemaObj is null)
		{
			// Use default
			url = $"https://learn.microsoft.com/{cultureCode.ToLower()}/windows/win32/adschema/";
		}
		else
		{
			string? prefix;
			if (schemaObj.ObjectClass.LdapDisplayName == SpecialClassNames.AttributeSchema)
			{
				prefix = "a";
			}
			else if (schemaObj.ObjectClass.LdapDisplayName == SpecialClassNames.ClassSchema)
			{
				prefix = "c";
			}
			else
			{
				prefix = null;
			}

			if (prefix != null)
			{
				url = $"https://learn.microsoft.com/{cultureCode.ToLower()}/windows/win32/adschema/{prefix}-{schemaObj.LdapDisplayName.ToLower()}";
			}
		}
		if (!string.IsNullOrEmpty(url))
			Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
	}
}
