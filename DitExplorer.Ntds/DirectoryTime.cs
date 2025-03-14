using System.Text.RegularExpressions;
using static Microsoft.Isam.Esent.Interop.EnumeratedColumn;

namespace DitExplorer.Ntds;

/// <summary>
/// Describes a date and time within the directory.
/// </summary>
/// <remarks>
/// This type encapsulates a <see cref="DateTime"/> and interprets the <see cref="DateTime.MinValue"/>
/// and <see cref="DateTime.MaxValue"/> as empty and never, respectively.
/// </remarks>
public record struct DirectoryTime : IComparable<DirectoryTime>, IComparable, IFormattable
{
	private const string NeverName = "Never";
	internal const long NeverTicks = 0x7FFFFFFF_FFFFFFFF;

	public DirectoryTime(DateTime time, long ticks)
	{
		Time = time;
		Ticks = ticks;
	}

	public DateTime Time { get; }
	public long Ticks { get; }

	public static DirectoryTime Never => new DirectoryTime(DateTime.MaxValue, NeverTicks);
	public static DirectoryTime Empty => new DirectoryTime(DateTime.MinValue, 0);

	public override string ToString()
	{
		if (this.Ticks == NeverTicks)
			return NeverName;
		else if (this.Time == DateTime.MinValue)
			return string.Empty;
		else return this.Time.ToString();
	}

	public int CompareTo(DirectoryTime other)
		=> this.Time.CompareTo(other.Time);

	public int CompareTo(object? obj)
	{
		if (obj is DirectoryTime other)
			return this.Time.CompareTo(other.Time);
		else
			throw new ArgumentException("The other value is not a DirectoryTime.", nameof(other));
	}

	private static readonly Regex RelativePattern = new Regex(@"(?-i)(?<origin>@Now|@Today)(?i)(\s*(?<sign>\+|-)\s*(?<o>\d+(\.\d*)?)(?<u>[smhd]))?");
	public static DirectoryTime Parse(string text, DateTimeKind kind, int factor)
	{
		if (text is null) throw new ArgumentNullException(nameof(text));
		if (text.Length == 0)
			return Empty;
		else if (text == NeverName)
			return Never;
		else if (int.TryParse(text, out var ticks))
		{
			var fileTime = ticks * factor;
			DateTime dt =
				(kind == DateTimeKind.Local) ? DateTime.FromFileTime(fileTime)
				: DateTime.FromFileTimeUtc(fileTime);
			return new DirectoryTime(dt, ticks);
		}
		else
		{
			Match m = RelativePattern.Match(text);
			if (m.Success)
			{
				var origin = m.Groups["origin"].Value.ToUpper();
				DateTime dt =
					origin == "@NOW" ? (kind == DateTimeKind.Utc ? DateTime.UtcNow : DateTime.Now)
					: DateTime.Today;

				var units = m.Groups["u"];
				if (units.Success)
				{
					double offsetValue = double.Parse(m.Groups["o"].Value);
					TimeSpan offset = units.Value switch
					{
						"s" => TimeSpan.FromSeconds(offsetValue),
						"m" => TimeSpan.FromMinutes(offsetValue),
						"h" => TimeSpan.FromHours(offsetValue),
						_ => TimeSpan.FromDays(offsetValue)
					};

					bool sign = m.Groups["sign"].Value == "-";
					dt = sign ? (dt - offset) : (dt + offset);
				}
				return new DirectoryTime(dt, (kind == DateTimeKind.Local ? dt.ToFileTime() : dt.ToFileTimeUtc()) / factor);
			}
			else
			{
				DateTime dt = DateTime.Parse(text);
				return new DirectoryTime(dt, (kind == DateTimeKind.Local ? dt.ToFileTime() : dt.ToFileTimeUtc()) / factor);
			}
		}
	}

	public string ToString(string? format, IFormatProvider? formatProvider)
	{
		if (string.IsNullOrEmpty(format) || format == "G")
			return this.ToString();
		if (format == "N")
			return this.Ticks.ToString("G");
		else if (format == "X")
			return this.Ticks.ToString("X");
		else
			return Time.ToString(format, formatProvider);
	}
}
