using UnityEngine;

public class SelfAssignIDAttribute : PropertyAttribute
{
	public Visibility ShowOrHide;

	public bool AssignOnce;

	public SelfAssignIDAttribute(Visibility _vis = Visibility.Show)
	{
		ShowOrHide = _vis;
	}

	public SelfAssignIDAttribute(bool _assignOnce)
	{
		AssignOnce = _assignOnce;
	}
}
