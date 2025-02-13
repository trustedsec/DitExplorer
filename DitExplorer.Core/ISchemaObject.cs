namespace DitExplorer;

public interface ISchemaObject : IDirectoryObject
{
	string LdapDisplayName { get; }
}