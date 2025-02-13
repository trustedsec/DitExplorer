

namespace DitExplorer.Ntds;

/// <summary>
/// Represents an object class in the directory.
/// </summary>
public sealed class NtdsClassSchema : NtdsSchemaObject, IClassSchema
{
	internal NtdsClassSchema(NtdsDirectory dir, int dnt, int parentDnt) : base(dir, dnt, parentDnt)
	{
	}

	/// <summary>
	/// Gets the encoded form of the governsID attribute.
	/// </summary>
	public int GovernsIdRaw { get; internal set; }
	/// <summary>
	/// Gets the DNT of the superclass of this class.
	/// </summary>
	internal int SubclassOfId { get; set; }

	internal int[] AuxiliaryClassIds { get; set; }
	internal int[] SystemAuxiliaryClassIds { get; set; }
	internal int[] MustContainIds { get; set; }
	internal int[] SystemMustContainIds { get; set; }
	internal int[] MayContainIds { get; set; }
	internal int[] SystemMayContainIds { get; set; }

	public NtdsClassSchema? SuperClass { get; internal set; }
	IClassSchema? IClassSchema.Superclass => this.SuperClass;

	public IAttributeSchema[] GetAttributes(bool includeBaseClasses)
	{
		var attrIds = this.GetAttributeIds(includeBaseClasses);
		var attrs = Array.ConvertAll(attrIds, r => this.Directory.GetAttributeById(r));
		return attrs;
	}
	public int[] GetAttributeIds(bool includeBaseClasses)
	{
		// TODO: Cache for later

		HashSet<int> attributes = new HashSet<int>();
		if (!includeBaseClasses)
		{
			GetAttributesInto(attributes);
			return attributes.ToArray();
		}

		HashSet<int> classDnts = new HashSet<int>();
		Queue<NtdsClassSchema?> baseClassQueue = new Queue<NtdsClassSchema?>();
		baseClassQueue.Enqueue(this);

		while (baseClassQueue.Count > 0)
		{
			var objcls = baseClassQueue.Dequeue();
			if (objcls != null && classDnts.Add(objcls.Dnt))
			{
				objcls.GetAttributesInto(attributes);
				baseClassQueue.Enqueue(objcls.SuperClass);

				foreach (var auxClassId in this.AuxiliaryClassIds)
				{
					var auxcls = this.Directory.GetClassByGovernsId(auxClassId);
					if (auxcls != null)
						baseClassQueue.Enqueue(auxcls);
				}
			}
		}

		return attributes.ToArray();
	}

	private void GetAttributesInto(HashSet<int> attributes)
	{
		attributes.AddRange(this.SystemMustContainIds);
		attributes.AddRange(this.MustContainIds);
		attributes.AddRange(this.SystemMayContainIds);
		attributes.AddRange(this.MayContainIds);
	}

	public bool HasAttribute(string ldapName)
	{
		var attr = this.Directory.TryGetAttributeByLdapName(ldapName);
		if (attr == null)
			return false;

		var objcls = this;
		HashSet<int> attrIds = new HashSet<int>();
		do
		{
			objcls.GetAttributesInto(attrIds);
			if (attrIds.Contains(attr.AttributeIdRaw))
				return true;

			objcls = objcls.SuperClass;
		} while (objcls is not null);

		return false;
	}
}