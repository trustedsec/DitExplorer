using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DitExplorer;

/// <summary>
/// Exposes functionality for interacting with a directory.
/// </summary>
public interface IDirectory : IDisposable
{
	/// <summary>
	/// Gets the root domain of the directory.
	/// </summary>
	IDirectoryObject RootDomain { get; }

	/// <summary>
	/// Gets a class corresponding to an encoded governsID value.
	/// </summary>
	/// <param name="governsId">Prefix-encoded governsID</param>
	/// <returns>An <see cref="IClassSchema"/> with <paramref name="governsId"/></returns>
	/// <exception cref="ArgumentException">No class found with <paramref name="governsId"/></exception>
	IClassSchema GetClassByGovernsId(int governsId);
	/// <summary>
	/// Gets a list of all object classes.
	/// </summary>
	/// <returns>An array of <see cref="IClassSchema"/> objects, one for
	/// each class defined in the directory..</returns>
	IClassSchema[] GetClassSchemas();
	/// <summary>
	/// Gets a list of all attribute in the schemae.
	/// </summary>
	/// <returns>An array of <see cref="IAttributeSchema"/> objects.</returns>
	IAttributeSchema[] GetAllAttributeSchemas();
	/// <summary>
	/// Gets an attribute by its LDAP name.
	/// </summary>
	/// <param name="name">LDAP name of attribute</param>
	/// <returns>The <see cref="IAttributeSchema"/> with the LDAP name <paramref name="name"/>, if found;
	/// otherwise, <see langword="null"/>.</returns>
	IAttributeSchema? TryGetAttributeByLdapName(string name);
}
