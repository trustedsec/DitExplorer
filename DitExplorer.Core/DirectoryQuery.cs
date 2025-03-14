using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer;
public sealed class DirectoryQuery
{
	public DirectoryQuery(DirectoryQueryClause filter)
	{
		if (filter is null) throw new ArgumentNullException(nameof(filter));
		Filter = filter;
	}

	public DirectoryQueryClause Filter { get; }
}

public abstract class DirectoryQueryClause
{
	private protected DirectoryQueryClause() { }

	protected abstract bool TestObjectImpl(IDirectoryObject obj);
	public bool TestObject(IDirectoryObject obj)
	{
		ArgumentNullException.ThrowIfNull(obj);
		return this.TestObjectImpl(obj);
	}
}

public enum ComparisonOperatorType
{
	EqualTo,
	NotEqualTo,
	GreaterThan,
	GreaterThanOrEqualTo,
	LessThan,
	LessThanOrEqualTo,
	HasFlag,
	DoesntHaveFlag,
	HasAllFlags,

	IsPresent,
	IsNotPresent,
}


public enum QueryConjunctionType
{
	All,
	Any
}
public sealed class ConjunctionClause : DirectoryQueryClause
{
	public ConjunctionClause(
		QueryConjunctionType conjunction,
		DirectoryQueryClause[] clauses
		)
	{
		if (clauses == null || clauses.Length == 0 || clauses.Contains(null))
			throw new ArgumentNullException(nameof(clauses));

		Conjunction = conjunction;
		Clauses = clauses;
	}

	public QueryConjunctionType Conjunction { get; }
	public DirectoryQueryClause[] Clauses { get; }

	protected sealed override bool TestObjectImpl(IDirectoryObject obj)
	{
		var conj = this.Conjunction;
		foreach (var clause in this.Clauses)
		{
			bool matches = clause.TestObject(obj);
			if (matches && conj == QueryConjunctionType.Any)
				return true;
			else if (!matches && conj == QueryConjunctionType.All)
				return false;
		}

		return true;
	}
}

public sealed class AttributeClause : DirectoryQueryClause
{
	public AttributeClause(
		IAttributeSchema attribute,
		ComparisonOperatorType comparison,
		object? value)
	{
		if (attribute is null) throw new ArgumentNullException(nameof(attribute));

		Attribute = attribute;
		Comparison = comparison;
		Value = value;
	}

	public IAttributeSchema Attribute { get; }
	public ComparisonOperatorType Comparison { get; }
	public object? Value { get; }

	protected sealed override bool TestObjectImpl(IDirectoryObject obj)
	{
		var attr = this.Attribute;
		var op = this.Comparison;
		var criterion = this.Value;
		if (attr.IsSingleValued)
		{
			var value = obj.GetValueOf(attr);
			return CompareValue(attr, value, op, criterion);
		}
		else
		{
			var multi = obj.GetMultiValuesOf(attr);
			if (multi == null)
				return (op == ComparisonOperatorType.IsNotPresent);
			else if (op == ComparisonOperatorType.IsPresent)
				return true;

			foreach (var value in multi)
			{
				if (CompareValue(attr, value, op, criterion))
					return true;
			}

			return false;
		}
	}

	public static bool CompareValue(
		IAttributeSchema attr,
		object? value,
		ComparisonOperatorType op,
		object? criterion)
	{
		if (value == null)
			return (op == ComparisonOperatorType.IsNotPresent);
		if (op == ComparisonOperatorType.IsPresent)
			return (value != null);

		return op switch
		{
			ComparisonOperatorType.IsPresent => true,
			ComparisonOperatorType.EqualTo => attr.Syntax.EqualityComparer.Equals(value, criterion),
			ComparisonOperatorType.NotEqualTo => !attr.Syntax.EqualityComparer.Equals(value, criterion),
			ComparisonOperatorType.HasFlag => HasFlag(value, criterion),
			ComparisonOperatorType.DoesntHaveFlag => !HasAllFlags(value, criterion),
			ComparisonOperatorType.HasAllFlags => HasAllFlags(value, criterion),
			ComparisonOperatorType.GreaterThan
			or ComparisonOperatorType.GreaterThanOrEqualTo
			or ComparisonOperatorType.LessThan
			or ComparisonOperatorType.LessThanOrEqualTo => CompareRelative(attr, op, value, criterion)
		};
	}

	private static bool HasFlag(object value, object? criterion)
	{
		var value64 = Convert.ToInt64(value);
		var criterion64 = Convert.ToInt64(criterion);
		return 0 != (value64 & criterion64);
	}

	private static bool HasAllFlags(object value, object? criterion)
	{
		var value64 = Convert.ToInt64(value);
		var criterion64 = Convert.ToInt64(criterion);
		return criterion64 == (value64 & criterion64);
	}

	internal static bool CompareRelative(
		IAttributeSchema attr,
		ComparisonOperatorType op,
		object value,
		object comparand)
	{
		int cmp = attr.Syntax.Compare(value, comparand);
		return op switch
		{
			ComparisonOperatorType.GreaterThan => cmp > 0,
			ComparisonOperatorType.GreaterThanOrEqualTo => cmp >= 0,
			ComparisonOperatorType.LessThan => cmp < 0,
			ComparisonOperatorType.LessThanOrEqualTo => cmp <= 0
		};
	}
}
