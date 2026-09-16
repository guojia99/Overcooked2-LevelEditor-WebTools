using UnityEngine;

public class AssignComponentAttribute : PropertyAttribute
{
	public enum LocatorType
	{
		Owner = 0,
		DirectChild = 1,
		Recursive = 2
	}

	public LocatorType Type;

	public Visibility ShowOrHide;

	public Editorbility CanEdit = Editorbility.NonEditable;

	public AssignComponentAttribute(Visibility _vis)
	{
		ShowOrHide = _vis;
	}

	public AssignComponentAttribute(Editorbility _editor = Editorbility.NonEditable)
	{
		CanEdit = _editor;
		ShowOrHide = ((_editor != Editorbility.Editable) ? ShowOrHide : Visibility.Show);
	}
}
