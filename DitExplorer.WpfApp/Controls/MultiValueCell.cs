using DitExplorer.Ntds;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DitExplorer.UI.WpfApp.Controls;
internal class MultiValueCell : ContentControl
{
	static MultiValueCell()
	{
		DefaultStyleKeyProperty.OverrideMetadata(typeof(MultiValueCell), new FrameworkPropertyMetadata(typeof(MultiValueCell)));
	}

	protected override void OnContentChanged(object oldContent, object newContent)
	{
		var multi = newContent as MultiValue;
		HasMultipleValues = multi != null && multi.Count > 1;
		base.OnContentChanged(oldContent, newContent);
	}

	public object? CommandParameter
	{
		get { return (object?)GetValue(CommandParameterProperty); }
		set { SetValue(CommandParameterProperty, value); }
	}
	public static readonly DependencyProperty CommandParameterProperty =
		DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(MultiValueCell), new PropertyMetadata(null));

	public bool HasMultipleValues
	{
		get { return (bool)GetValue(HasMultipleValuesProperty); }
		private set { SetValue(HasMultipleValuesPropertyKey, value); }
	}
	private static readonly DependencyPropertyKey HasMultipleValuesPropertyKey =
		DependencyProperty.RegisterReadOnly(nameof(HasMultipleValues), typeof(bool), typeof(MultiValueCell), new PropertyMetadata(false));
	public static readonly DependencyProperty HasMultipleValuesProperty = HasMultipleValuesPropertyKey.DependencyProperty;


}
