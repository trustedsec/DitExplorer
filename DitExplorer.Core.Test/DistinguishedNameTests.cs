namespace DitExplorer.Core.Test;

[TestClass]
public class DistinguishedNameTests
{
	[TestMethod]
	[DataRow("CN=name", "CN", "name")]
	[DataRow("CN=Kier\\, PE", "CN", "Kier, PE")]
	public void TestSimple(string text, string attrName, string value)
	{
		var dn = DistinguishedName.Parse(text);
		Assert.AreEqual(1, dn.Parts.Length);

		DistinguishedNamePart part0 = dn.Parts[0];
		Assert.AreEqual(attrName, part0.Attribute);
		Assert.AreEqual(value, part0.Value);
	}

	[TestMethod]
	[DataRow(new object[] { "CN=Administrator,CN=Users,DC=lumon,DC=ind", new string[] { "CN", "CN", "DC", "DC" }, new string[] { "Administrator", "Users", "lumon", "ind" } }, DisplayName = "Administrator")]
	public void TestMulti(string text, string[] attrs, string[] values)
	{
		var dn = DistinguishedName.Parse(text);

		for (int i = 0; i < attrs.Length; i++)
		{
			var part = dn.Parts[i];
			var attr = attrs[i];
			var value = values[i];
			Assert.AreEqual(attr, part.Attribute);
			Assert.AreEqual(value, part.Value);
		}
	}
}