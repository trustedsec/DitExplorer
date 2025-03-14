## Added

### General
* Display more fields as date/time
* Display min/max as empty / Never
* Search directory by distinguished name
* Support <SID=x>, <GUID=x>, <DNT=x> syntaxes

### Main Window
* AD Schema Help for schema objects
* Export Subtree as XML action
* Support Object(DN-Binary) syntax
* Support enumerations: groupType, samAccountType, userAccountControl, schemaFlagsEx
* Support for multi-domain database
* OID attributes resolve to schema objects when possible (e.g. subClassOf, mayContain)
* Int64 timestamp files now display as a date
* Display format options for attribute columns

### Search
* Search by GUID
* Search by SID
* Search by attributes
* Relative date syntax

### Object Inspector
* Copy Value for each individual cell instead of entire row
* AD schema help

### Credential Extractor
* Result list shows logon name and UPN
* Extracts WDigest hashes, Kerberos keys, cleartext password, and password history
* Export remembers last file type selection
* After exporting, Credential Extractor offer to open the folder containing the exported file.
* Extract system key from registry hive file

## Fixes
* Resolved an issue when reading multiple values from a byte field
* Int64 syntax had the wrong omSyntax
