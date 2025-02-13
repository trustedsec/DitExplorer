namespace DitExplorer;

public interface IAttributeSchema : ISchemaObject
{
	IAttributeSyntax? Syntax { get; }
	bool IsSingleValued { get; }
	bool IsLink { get; }
}