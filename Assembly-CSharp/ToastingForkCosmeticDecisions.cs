using System.Collections.Generic;
using UnityEngine;

public class ToastingForkCosmeticDecisions : MonoBehaviour
{
	[SerializeField]
	[ReadOnly]
	public float m_surfaceAttachedOffset = -0.48f;

	public void ApplyPositionOffsetToChilden(Vector3 _offset, ref Dictionary<string, Vector3> _originalPositions)
	{
		int childCount = base.transform.childCount;
		for (int i = 0; i < childCount; i++)
		{
			Transform child = base.transform.GetChild(i);
			_originalPositions.SafeAdd(child.name, child.localPosition);
			child.localPosition += _offset;
		}
	}

	public void RestorePositionsToChildren(ref Dictionary<string, Vector3> _originalPositions)
	{
		int childCount = base.transform.childCount;
		for (int i = 0; i < childCount; i++)
		{
			Transform child = base.transform.GetChild(i);
			Vector3 localPosition = _originalPositions.SafeGet(child.name, child.localPosition);
			child.localPosition = localPosition;
		}
	}
}
