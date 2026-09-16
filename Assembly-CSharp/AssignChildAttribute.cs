using UnityEngine;

public class AssignChildAttribute : PropertyAttribute
{
	public string Name;

	public Editorbility Editable = Editorbility.NonEditable;

	public AssignChildAttribute(string _name, Editorbility _canEdit = Editorbility.NonEditable)
	{
		Name = _name;
		Editable = _canEdit;
	}
}
