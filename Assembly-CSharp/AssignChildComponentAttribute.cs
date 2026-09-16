public class AssignChildComponentAttribute : AssignComponentAttribute
{
	public AssignChildComponentAttribute(Visibility _vis)
		: base(_vis)
	{
		Type = LocatorType.DirectChild;
	}

	public AssignChildComponentAttribute(Editorbility _e = Editorbility.NonEditable)
		: base(_e)
	{
		Type = LocatorType.DirectChild;
	}
}
