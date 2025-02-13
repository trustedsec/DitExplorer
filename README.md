# DIT Explorer
DIT Explorer is a Windows application written in C# for browsing a NTDS.dit file.
Created in Visual Studio 2022.

# Building
To build the application:

1. Open DitExplorer.sln with Visual Studio 2022.
2. Build and run DitExplorer.UI.WpfApp

# Using the Application
From the main application window, open a .DIT file using File > Open DIT File
After opening a directory database, DIT Explorer displays the hierarchy of the domain on the left-hand side and the contents of the selected node in the right-hand pane.

* To view the attributes, members, and groups of an object, double-click it or right-click > Properties.
* To view the database schema, select Tools > Database Schema
* To view the directory schema, navigate to Configuration\Schema under the domain.
* To search a subtree, right-click the node at the root of the subtree, then select Search Subtree.
* To change the attributes shown as columns, select View > Columns, or right-click the list view and select Columns...
* To copy one or more items to the clipboard, highlight them, then select one of the Copy commands.
* To dump hashes, select one or more accounts, then right-click and select Extract Credentials

Most of the lists support sorting by clicking the column headers and searching by typing directly into the list.
