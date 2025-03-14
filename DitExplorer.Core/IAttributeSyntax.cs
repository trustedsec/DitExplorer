using System.Collections;

namespace DitExplorer;

public interface IAttributeSyntax : IComparer
{
	string LdapName { get; }
	Type? AttributeType { get; }
	bool CanRetrieveValue { get; }

	bool SupportsParse { get; }
	object? ParseValue(string text);

	bool SupportsFlags { get; }
	bool SupportsComparison { get; }
	IEqualityComparer EqualityComparer { get; }
	//IComparer? Comparer { get; }

	bool IsFormattable { get; }
	AttributeFormat[]? GetDisplayFormats();
}

/// <summary>
/// Describes an attribute display format.
/// </summary>
/// <param name="DisplayName">Name of the format</param>
/// <param name="Code">Format code</param>
public record struct AttributeFormat(string DisplayName, string FormatString);
