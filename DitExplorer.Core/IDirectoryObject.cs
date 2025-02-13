
namespace DitExplorer;

/// <summary>
/// Provides functionality for interacting with an object in a directory.
/// </summary>
public interface IDirectoryObject
{
	/// <summary>
	/// Gets the name of the object.
	/// </summary>
	public string Name { get; }
	string DistinguishedName { get; }
	/// <summary>
	/// Gets the directory containing the object.
	/// </summary>
	IDirectory Directory { get; }
	/// <summary>
	/// Gets the path of the object.
	/// </summary>
	/// <remarks>
	/// The value of this property resembles a file path with ancestor names separated by a backslash.
	/// </remarks>
	/// <seealso cref="DistinguishedName"/>
	string ObjectPath { get; }

	IDirectoryObject? Parent { get; }
	IDirectoryObject GetChild(string name);
	IEnumerable<IDirectoryObject> GetChildren();
	object? GetValueOf(IAttributeSchema attribute);
	MultiValue GetMultiValuesOf(IAttributeSchema attribute);
	object[] GetValueOfMultiple(IList<IAttributeSchema> attributes);
	IEnumerable<IDirectoryObject> SearchSubtree(string? searchName);
	IEnumerable<IDirectoryObject> SearchSubtree(string? searchName, IClassSchema? objectClass, bool includeSubclasses);

	/// <summary>
	/// Gets the object class schema.
	/// </summary>
	IClassSchema ObjectClass { get; }

	/// <summary>
	/// Gets a list of objects that are direct members of this object.
	/// </summary>
	/// <returns></returns>
	IEnumerable<IDirectoryObject> GetMembers();
	/// <summary>
	/// Gets a list of objects that this object is a member of.
	/// </summary>
	/// <returns></returns>
	IEnumerable<IDirectoryObject> GetMemberOfGroups();
}