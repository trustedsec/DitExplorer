using DitExplorer.Ntds;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.UI.WpfApp;

/// <summary>
/// Represents a directory attribute as a property.
/// </summary>
internal sealed class DirectoryPropertyDescriptor : PropertyDescriptor
{
	private readonly object UnsupportedValue = "<Value type unsupported>";

	public IAttributeSchema AttributeSchema { get; }

	private static Attribute[] GetAttributesFor(IAttributeSchema attrSchema)
	{
		List<Attribute>? attrs = null;

		if (attrSchema is NtdsAttributeSchema ntdsAttr)
		if (!string.IsNullOrEmpty(ntdsAttr.AdminDescription))
			(attrs = new List<Attribute>()).Add(new DescriptionAttribute(ntdsAttr.AdminDescription));

		if (attrs == null || attrs.Count == 0)
			return Array.Empty<Attribute>();
		else
			return attrs.ToArray();
	}
	internal DirectoryPropertyDescriptor(IAttributeSchema attributeSchema)
		: base(attributeSchema.LdapDisplayName, GetAttributesFor(attributeSchema))
	{
		AttributeSchema = attributeSchema;
	}

	/// <inheritdoc/>
	public sealed override Type ComponentType => typeof(DirectoryNode);
	/// <inheritdoc/>
	public sealed override bool IsReadOnly => true;

	private Type? _propertyType;
	/// <inheritdoc/>
	public sealed override Type PropertyType => _propertyType ??= MakeType();
	private Type MakeType()
	{
		if (AttributeSchema.IsLink)
			return AttributeSchema.IsSingleValued ? typeof(NtdsDirectoryObjectReference) : typeof(MultiValue<NtdsDirectoryObjectReference>);
		else
		{
			var type = AttributeSchema.Syntax?.AttributeType ?? typeof(object);
			if (!AttributeSchema.IsSingleValued)
				type = typeof(MultiValue<>).MakeGenericType(type);

			return type;
		}
	}
	/// <inheritdoc/>
	public sealed override bool CanResetValue(object component) => false;
	/// <inheritdoc/>
	public sealed override object? GetValue(object? component)
	{
		if (component is null)
			return null;

		var syntax = AttributeSchema.Syntax;
		if (syntax == null || !syntax.CanRetrieveValue)
			return UnsupportedValue;

		var node = (DirectoryNode)component;
		if (AttributeSchema.IsSingleValued)
			return node.Object.GetValueOf(AttributeSchema);
		else
		{
			var multi = node.Object.GetMultiValuesOf(AttributeSchema);
			return multi;
		}
	}

	/// <inheritdoc/>
	public sealed override void ResetValue(object component)
	{
		throw new NotImplementedException();
	}

	/// <inheritdoc/>
	public sealed override void SetValue(object? component, object? value)
	{
		throw new NotImplementedException();
	}

	/// <inheritdoc/>
	public sealed override bool ShouldSerializeValue(object component)
	{
		throw new NotImplementedException();
	}
}
