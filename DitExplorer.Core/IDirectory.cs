using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
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

	/// <summary>
	/// Gets an object by its distinguished name.
	/// </summary>
	/// <param name="dn">DN of object</param>
	/// <returns>The object identified by <paramref name="dn"/>.</returns>
	IDirectoryObject GetObjectByDn(DistinguishedName dn);
	/// <summary>
	/// Gets an object by its distinguished name.
	/// </summary>
	/// <param name="dn">DN of object</param>
	/// <returns>The object identified by <paramref name="dn"/>.</returns>
	IDirectoryObject GetObjectByDn(string dn);
	/// <summary>
	/// Gets an object with the specified GUID.
	/// </summary>
	/// <param name="guid">GUID to search for</param>
	/// <returns>The <see cref="IDirectoryObject"/> with objectGuid equal to <paramref name="guid"/>.</returns>
	/// <exception cref="DirectoryObjectNotFoundException">No object found with <paramref name="guid"/>.</exception>
	IDirectoryObject GetObjectByGuid(Guid guid);
	/// <summary>
	/// Gets an object with the specified SID.
	/// </summary>
	/// <param name="sid">SID to search for</param>
	/// <returns>The <see cref="IDirectoryObject"/> with objectSid equal to <paramref name="sid"/>.</returns>
	/// <exception cref="DirectoryObjectNotFoundException">No object found with <paramref name="sid"/>.</exception>
	IDirectoryObject GetObjectBySid(SecurityIdentifier sid);
}
