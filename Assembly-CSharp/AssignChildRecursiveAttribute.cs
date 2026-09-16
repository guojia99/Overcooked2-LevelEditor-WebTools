public class AssignChildRecursiveAttribute : AssignChildAttribute
{
	public AssignChildRecursiveAttribute(string _name, Editorbility _canEdit = Editorbility.NonEditable)
		: base(_name, _canEdit)
	{
	}
}
