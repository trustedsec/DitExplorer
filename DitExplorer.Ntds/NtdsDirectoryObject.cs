
using System;
using System.Text;
using static Microsoft.Isam.Esent.Interop.EnumeratedColumn;

namespace DitExplorer.Ntds;

[Flags]
enum DirectoryObjectFlags
{
	None = 0,

	IsDeleted = 1,
}
/// <summary>
/// Represents an object in the directory.
/// </summary>
public class NtdsDirectoryObject : IDirectoryObject
{
	internal NtdsDirectoryObject(NtdsDirectory dir, int dnt, int parentDnt)
	{
		Directory = dir;
		Dnt = dnt;
		ParentDnt = parentDnt;
	}

	// TODO: Implement attribute value caching

	internal DirectoryObjectFlags _flags;
	internal int[] _superclassChain;
	internal int _objClassGovernsId;
	private NtdsClassSchema? _objClass;
	private NtdsDirectoryObject? _parent;
	internal byte[] _ancestryBytes;
	internal int[] _ancestryDnts;
	internal int _rdnAttrId;

	/// <inheritdoc/>
	public override string ToString() => this.DistinguishedName;

	internal string GetRdnTypeToken()
		=> ((this._rdnAttrId != 0) ? this.Directory.TryGetAttributeById(this._rdnAttrId) : null)
		?.RdnToken ?? "CN";

	private string? _dn;
	/// <summary>
	/// Gets the distinguished name of the object.
	/// </summary>
	public string DistinguishedName => (this._dn ??= this.BuildDN());
	private static string EscapeRdn(string rdn)
		=> rdn.Replace("\r", @"\r")
		.Replace("\n", @"\n")
		.Replace(",", @"\,");
	private string BuildDN()
	{
		StringBuilder sb = new StringBuilder();

		sb.Append(this.GetRdnTypeToken())
			.Append('=')
			.Append(EscapeRdn(this.Name));

		for (int i = this._ancestryDnts.Length - 1; i >= 0; i--)
		{
			int ancestorDnt = this._ancestryDnts[i];
			if (ancestorDnt < 3)
				// Skip $ROOT_OBJECT$
				break;

			var ancestor = this.Directory.GetByDnt(ancestorDnt);

			sb.Append(',')
				.Append(ancestor.GetRdnTypeToken())
				.Append('=')
				.Append(EscapeRdn(ancestor.Name));
		}

		return sb.ToString();
	}

	private string? _objectPath;
	public string ObjectPath => (this._objectPath ??= this.BuildObjectPath());
	private string BuildObjectPath()
	{
		StringBuilder sb = new StringBuilder();

		for (int i = 0; i < this._ancestryDnts.Length; i++)
		{
			int ancestorDnt = this._ancestryDnts[i];
			if (ancestorDnt < 3)
				// Skip $ROOT_OBJECT$
				continue;

			var ancestor = this.Directory.GetByDnt(ancestorDnt);

			if (sb.Length > 0)
				sb.Append('\\');
			sb.Append(ancestor.Name);
		}

		sb.Append('\\').Append(this.Name);

		return sb.ToString();
	}

	/// <summary>
	/// Gets the object class schema.
	/// </summary>
	public NtdsClassSchema ObjectClass => _objClass ??= Directory.GetClassByGovernsId(_objClassGovernsId);
	/// <inheritdoc/>
	IClassSchema IDirectoryObject.ObjectClass => this.ObjectClass;

	/// <summary>
	/// Gets an <see cref="ADInstanceType"/> describing this object.
	/// </summary>
	public ADInstanceType InstanceType { get; internal set; }
	/// <summary>
	/// Gets a value indicating whether this is the head of a naming context.
	/// </summary>
	public bool IsNCHead => 0 != (InstanceType & ADInstanceType.HeadOfNamingContext);
	/// <summary>
	/// Gets a value indicating whether this is a deleted object.
	/// </summary>
	public bool IsDeleted => 0 != (_flags & DirectoryObjectFlags.IsDeleted);

	/// <summary>
	/// Gets the parent of this object, if any.
	/// </summary>
	public NtdsDirectoryObject? Parent => _parent ??= ParentDnt == 0 ? null : Directory.GetByDnt(ParentDnt);
	IDirectoryObject IDirectoryObject.Parent => this.Parent;

	/// <summary>
	/// Gets the directorty containing the object.
	/// </summary>
	public NtdsDirectory Directory { get; }
	IDirectory IDirectoryObject.Directory => this.Directory;

	/// <summary>
	/// Gets the distinguished name tag of the object.
	/// </summary>
	public int Dnt { get; }
	/// <summary>
	/// Gets the distinguished name tag of the object's parent.
	/// </summary>
	public int ParentDnt { get; }

	/// <summary>
	/// Gets the name of the object.
	/// </summary>
	public string Name { get; internal set; }

	/// <summary>
	/// Gets a child object by name.
	/// </summary>
	/// <param name="name">Name of child object</param>
	/// <returns>A <see cref="NtdsDirectoryObject"/> representing the child.</returns>
	/// <exception cref="ArgumentException"><paramref name="name"/> is <see langword="null"/> or empty.</exception>
	public NtdsDirectoryObject GetChild(string name)
	{
		if (string.IsNullOrEmpty(name)) throw new ArgumentException($"'{nameof(name)}' cannot be null or empty.", nameof(name));

		return Directory.GetByParentDntName(Dnt, name);
	}
	/// <inheritdoc/>
	IDirectoryObject IDirectoryObject.GetChild(string name) => this.GetChild(name);

	/// <summary>
	/// Gets the children of this object.
	/// </summary>
	/// <returns>A <see cref="DirectoryEnumerable"/> that enumerates children of this object.</returns>
	public DirectoryEnumerable GetChildren()
		=> Directory.GetChildrenOf(Dnt);
	IEnumerable<IDirectoryObject> IDirectoryObject.GetChildren() => this.GetChildren();

	/// <summary>
	/// Gets a single value of an attribute.
	/// </summary>
	/// <param name="attribute">Attribute to retrieve</param>
	/// <param name="tag">1-based index of value to return</param>
	/// <returns>Value of <paramref name="attribute"/> set on this object</returns>
	/// <exception cref="ArgumentNullException"><paramref name="attribute"/> is <see langword="null"/>.</exception>
	/// <remarks>
	/// If no value is set, this method returns <see langword="null"/>.
	/// </remarks>
	public object? GetValueOf(NtdsAttributeSchema attribute, int tag, bool decode = true)
	{
		if (attribute is null) throw new ArgumentNullException(nameof(attribute));
		return this.Directory.GetAttributeValueFor(this, attribute, tag, decode);
	}
	object? IDirectoryObject.GetValueOf(DitExplorer.IAttributeSchema attribute)
		=> this.GetValueOf(ArgumentHelper.ThrowIfNot<NtdsAttributeSchema>(attribute), 1);

	/// <summary>
	/// Gets all values of a multi-valued attribute.
	/// </summary>
	/// <param name="attribute">Attribute to retrieve</param>
	/// <returns>A <see cref="MultiValue{T}"/> containing the attribute values</returns>
	/// <exception cref="ArgumentNullException"><paramref name="attribute"/> is <see langword="null"/></exception>
	public MultiValue GetMultiValuesOf(NtdsAttributeSchema attribute)
	{
		if (attribute is null) throw new ArgumentNullException(nameof(attribute));
		if (attribute.IsSingleValued) throw new ArgumentException(Messages.DirectoryObject_AttrIsSingleValued, nameof(attribute));
		return this.Directory.GetAttributeMultiValuesFor(this, attribute);
	}
	MultiValue IDirectoryObject.GetMultiValuesOf(IAttributeSchema attribute)
		=> this.GetMultiValuesOf(ArgumentHelper.ThrowIfNot<NtdsAttributeSchema>(attribute));

	public object[] GetValueOfMultiple(IList<NtdsAttributeSchema> attributes, bool decode = true)
	{
		// Argument validation deferred to the called method
		return this.Directory.GetAttributeValuesFor(this, attributes, decode);
	}
	object[] IDirectoryObject.GetValueOfMultiple(IList<IAttributeSchema> attributes)
		=> this.GetValueOfMultiple(attributes.ThrowIfNot<IAttributeSchema, NtdsAttributeSchema>());

	public void SetAttributeRawValue(NtdsAttributeSchema attribute, object? value)
	{
		if (attribute is null) throw new ArgumentNullException(nameof(attribute));

		this.Directory.SetAttributeRawValueFor(this, attribute, value);
	}

	public void SetParent(int dnt)
	{
		this.Directory.SetParentDntOf(this, dnt);
	}

	public IEnumerable<NtdsDirectoryObject> SearchSubtree(string? searchName)
		=> this.SearchSubtree(searchName, null, false);
	public IEnumerable<NtdsDirectoryObject> SearchSubtree(string? searchName, NtdsClassSchema? objectClass, bool includeSubclasses)
	{
		return this.Directory.SearchSubtree(this, searchName, objectClass, includeSubclasses);
	}
	IEnumerable<IDirectoryObject> IDirectoryObject.SearchSubtree(string? searchName)
		=> this.SearchSubtree(searchName);
	IEnumerable<IDirectoryObject> IDirectoryObject.SearchSubtree(string? searchName, IClassSchema? objectClass, bool includeSubclasses)
		=> this.SearchSubtree(searchName, ArgumentHelper.ThrowIfNotNullAndNot<NtdsClassSchema>(objectClass), includeSubclasses);

	public bool IsInstanceOf(NtdsClassSchema classSchema)
	{
		if (classSchema is null) throw new ArgumentNullException(nameof(classSchema));

		// TODO: Check if classSchema is from another directory?

		return (Array.IndexOf(this._superclassChain, classSchema.GovernsIdRaw) >= 0);
	}

	public IEnumerable<IDirectoryObject> GetMembers()
		=> this.Directory.GetMembersOf(this);

	public IEnumerable<IDirectoryObject> GetMemberOfGroups()
		=> this.Directory.GetMemberOfGroups(this);
}
