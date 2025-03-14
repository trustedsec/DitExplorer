using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer;

/// <summary>
/// Represents a distinguished name.
/// </summary>
public class DistinguishedName
{
	public DistinguishedName(DistinguishedNamePart[] parts)
	{
		if (parts == null || parts.Length == 0 || parts.Contains(null))
			throw new ArgumentNullException(nameof(parts));

		Parts = parts;
	}

	public DistinguishedNamePart[] Parts { get; }
	private string? _text;
	public string Text => (this._text ??= BuildText());

	private string BuildText()
	{
		return string.Join(",", this.Parts.Select(r => r.Text));
	}

	public static DistinguishedName Parse(string dn)
	{
		if (string.IsNullOrEmpty(dn)) throw new ArgumentException($"'{nameof(dn)}' cannot be null or empty.", nameof(dn));

		StringBuilder sb = new StringBuilder();
		List<DistinguishedNamePart> parts = new List<DistinguishedNamePart>();

		char lastChar = '\0';
		string? attrName = null;
		int hexCode = -1;
		int hexDigit;
		for (int i = 0; i <= dn.Length; i++)
		{
			char c = (i == dn.Length) ? '\0' : dn[i];

			if (attrName == null && c == '=' && lastChar != '\\')
			{
				attrName = sb.ToString();
				sb.Clear();
			}
			else if ((c is ',' or '\0') && lastChar != '\\')
			{
				if (attrName == null)
					throw new FormatException($"Attribute '{sb.ToString()}' does not have a value assigned.");

				var value = sb.ToString();
				sb.Clear();
				parts.Add(new DistinguishedNamePart(attrName, value));
				attrName = null;
			}
			else if (lastChar == '\\' && (-1 != (hexDigit = HexHelper.TryParseHexChar(c))))
			{
				if (hexCode == -1)
				{
					hexCode = hexDigit;
					continue;
				}
				else
				{
					hexCode <<= 8;
					hexCode |= hexDigit;
					sb.Append((char)hexCode);

					hexCode = 0;
				}
			}
			else if (c != '\\')
				sb.Append(c);

			lastChar = c;
		}

		return new DistinguishedName(parts.ToArray());
	}

	public static string Escape(string value)
	{
		if (value is null) throw new ArgumentNullException(nameof(value));

		StringBuilder sb = new StringBuilder();
		const string ReservedChars = ",+\"\\<>;=/";
		foreach (var c in value)
		{
			if (ReservedChars.Contains(c))
				sb.Append('\\').Append(c);
			else if (c == '\r')
				sb.Append("\\0D");
			else if (c == '\n')
				sb.Append("\\0A");
			else
				sb.Append(c);
		}

		return sb.ToString();
	}
}

public class DistinguishedNamePart
{
	public DistinguishedNamePart(string attribute, string value)
	{
		if (string.IsNullOrEmpty(attribute)) throw new ArgumentException($"'{nameof(attribute)}' cannot be null or empty.", nameof(attribute));
		if (string.IsNullOrEmpty(value)) throw new ArgumentException($"'{nameof(value)}' cannot be null or empty.", nameof(value));

		Attribute = attribute;
		Value = value;
	}

	private string? _text;
	public string Text => (this._text ??= BuildText());
	private string BuildText()
	{
		return this.Attribute + '=' + this.EscapedValue;
	}

	public override string ToString()
				=> this.Text;

	public string Attribute { get; }
	public string Value { get; }

	private string? _escapedValue;
	public string EscapedValue => (this._escapedValue ??= DistinguishedName.Escape(this.Value));
}