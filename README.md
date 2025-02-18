# DIT Explorer
DIT Explorer is a Windows application written in C# for browsing a NTDS.dit file.
Created in Visual Studio 2022.

I wrote this as a tool for researching the structure of NTDS.dit. For more
information, see my blog post at (coming soon)

![image](https://github.com/user-attachments/assets/8efd1f31-8d2d-4adc-9832-4b33c68b5dea)

# Building
To build the application:

1. Open DitExplorer.sln with Visual Studio 2022.
2. Build and run DitExplorer.UI.WpfApp

# Using the Application

From the main application window, open a .DIT file using File > Open DIT File.
DIT Explorer uses ManagedEsent to open the database.  If the file was pulled from a
shadow copy and is unclean, you may need to repair it before opening it with
DIT Explorer using `esent /p`.

After opening a directory database, DIT Explorer displays the hierarchy of the
domain on the left-hand side and the contents of the selected node in the right-hand pane.

* To view the attributes, members, and groups of an object, double-click it or right-click `> Properties`.
* To view the database schema, select `Tools > Database Schema`
* To view the directory schema, navigate to Configuration\Schema under the domain.
* To search a subtree, right-click the node at the root of the subtree, then select `Search Subtree`.
* To change the attributes shown as columns, select `View > Columns`, or right-click the list view and select `Columns...`
* To copy one or more items to the clipboard, highlight them, then select one of the Copy commands.
* To dump hashes, select one or more accounts, then right-click and select `Extract Credentials`

Most of the lists support sorting by clicking the column headers and searching by
typing directly into the list.

To perform an action (such as Extract Credential) on multiple objects within a
subtree, search the subtree, highlight the objects in the search results, then
right-click and select the desired action.

# Customizing the View

In both the main application window and the Search window, you may select which columns the list displays.
Right-click in the list, then select `Columns...`

![image](https://github.com/user-attachments/assets/cc40cce8-3deb-49ca-885e-4bb27e1ab3a9)

 Column Chooser allows you to select which schema attributes to display as a column in the list view.
 The `Column set` selection allows you to browse the attributes contained by a particular class.

# Searching the Directory

To search the directory:
1. Right-click on the node you want to search in, then select `Search Subtree`.
2. Enter part of the name of the object you wish to search for, or leave the Name field blank to find all objects in the subtree.  DIT Explorer searchs for the
search text within the attributes marked ANR to produce a similar experience to searching for a
user in Active Directory.
3. Optionally select the class of object to search for, optionally
including subclasses.  Leave the selection blank to search for all objects matching the name entered above.
4. Click `Search Now`.
5. Interact with the results by selecting and right-clicking them.

![image](https://github.com/user-attachments/assets/c7797618-b0ed-4c64-8cd0-ca939c73d8c7)

# Extracting Credentials

To extract credentials, you'll need the system key of the DC that you pulled the NTDS.dit file
from.

![image](https://github.com/user-attachments/assets/b930e108-ae30-4e5d-9fe0-c6f3d6e5686f)

1. Right-click the user or computer that you want to extract credentials from, then select `Extract Credentials`.
3. Enter the system key.
4. Click `Extract Credentials`.
5. Click `Export...` to export the credentials to a file.  DIT Explorer supports exporting to a tab-delimited text file, CSV, or pwdump-style text file.

# Viewing the Database Schema

![image](https://github.com/user-attachments/assets/c3969d0e-2aba-4dc1-8b7c-6d01f9e73cc6)

1. Open the .DIT file.
2. From the `Tools` menu, select `Database Schema`.

The right-hand pane lists the columns and indexes in the table.  You may highlight and copy a list of columns or indexes to the clipboard.  Use the `Export Table Data` button to export the raw data from the selected table.
