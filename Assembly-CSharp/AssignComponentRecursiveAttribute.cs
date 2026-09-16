public class AssignComponentRecursiveAttribute : AssignComponentAttribute
{
	public AssignComponentRecursiveAttribute(Visibility _vis)
		: base(_vis)
	{
		Type = LocatorType.Recursive;
	}

	public AssignComponentRecursiveAttribute(Editorbility _e = Editorbility.NonEditable)
		: base(_e)
	{
		Type = LocatorType.Recursive;
	}
}
