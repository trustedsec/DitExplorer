using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer.UI.WpfApp
{
	/// <summary>
	/// Writes formatted tabular data to a <see cref="TextWriter"/>.
	/// </summary>
	public abstract class TabularTextWriter
	{
		protected TabularTextWriter(TextWriter writer)
		{
			this.writer = writer;
		}

		protected readonly TextWriter writer;
		private bool _lineHasValue;

		/// <summary>
		/// Writes a value.
		/// </summary>
		/// <param name="text">Value to write</param>
		public void WriteValue(string text)
		{
			if (_lineHasValue)
				WriteSeparator();
			else
				_lineHasValue = true;

			if (!string.IsNullOrEmpty(text))
				WriteValueCore(text);
		}

		protected abstract char[] EscapedChars { get; }
		protected virtual void WriteValueCore(string text)
		{
			if (text.IndexOfAny(EscapedChars) >= 0)
			{
				if (text.Contains("\""))
					text = text.Replace("\"", "\"\"");

				// TODO: Yeah, this could be more efficient using the TextWriter properly

				text = '"' + text + '"';
			}

			writer.Write(text);
		}

		/// <summary>
		/// Writes the separator between values.
		/// </summary>
		protected abstract void WriteSeparator();
		/// <summary>
		/// Writes the end of record.
		/// </summary>
		public void EndRecord()
		{
			writer.WriteLine();
			_lineHasValue = false;
		}
	}
	/// <summary>
	/// Writes tabular data as tab-separated values.
	/// </summary>
	public sealed class TsvBuilder : TabularTextWriter
	{
		/// <summary>
		/// Initializes a new <see cref="TsvBuilder"/>
		/// </summary>
		/// <param name="writer">Underlying <see cref="TextWriter"/> to write to</param>
		public TsvBuilder(TextWriter writer) : base(writer)
		{
		}

		/// <inheritdoc/>
		protected sealed override void WriteSeparator()
		{
			writer.Write('\t');
		}


		private static readonly char[] escapeChars = new char[] { '\t', '\r', '\n' };
		protected override char[] EscapedChars => escapeChars;
	}
	/// <summary>
	/// Writes tabular data as comma-separated values.
	/// </summary>
	public sealed class CsvBuilder : TabularTextWriter
	{
		/// <summary>
		/// Initializes a new <see cref="CsvBuilder"/>
		/// </summary>
		/// <param name="writer">Underlying <see cref="TextWriter"/> to write to</param>
		public CsvBuilder(TextWriter writer) : base(writer)
		{
		}

		/// <inheritdoc/>
		protected sealed override void WriteSeparator()
		{
			writer.Write(',');
		}

		private static readonly char[] escapeChars = new char[] { ',', '\r', '\n' };
		protected override char[] EscapedChars => escapeChars;
	}
}
