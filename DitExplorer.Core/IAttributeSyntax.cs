namespace DitExplorer;

public interface IAttributeSyntax
{
	Type? AttributeType { get; }
	bool CanRetrieveValue { get; }
}