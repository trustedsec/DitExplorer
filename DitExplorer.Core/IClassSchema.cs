namespace DitExplorer;

public interface IClassSchema : ISchemaObject
{
	IClassSchema? Superclass { get; }
	bool HasAttribute(string ldapName);
	IAttributeSchema[] GetAttributes(bool includeBaseClasses);
}