using UnityEngine;

public static class RectTransformUtils
{
	public static Vector3 GetSize(this RectTransform _target)
	{
		Vector3[] array = new Vector3[4];
		_target.GetWorldCorners(array);
		Bounds bounds = new Bounds(array[0], Vector3.zero);
		for (int i = 1; i < 4; i++)
		{
			bounds.Encapsulate(array[i]);
		}
		return bounds.size;
	}
}
