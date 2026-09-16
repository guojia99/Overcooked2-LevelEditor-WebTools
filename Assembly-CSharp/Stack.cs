using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class Stack : MonoBehaviour, IParentable
{
	[SerializeField]
	public string m_itemTag;

	[SerializeField]
	public float m_heightOffset;

	private Transform m_AttachPoint;

	protected virtual void Awake()
	{
		if (m_AttachPoint == null)
		{
			GameObject gameObject = new GameObject("StackAttachment");
			m_AttachPoint = gameObject.transform;
			m_AttachPoint.SetParent(base.transform);
			m_AttachPoint.localPosition = default(Vector3);
			m_AttachPoint.localRotation = Quaternion.identity;
		}
	}

	public void RefreshStackTransforms(ref List<GameObject> _objects)
	{
		_objects.RemoveAll((GameObject x) => x == null);
		float num = 0f;
		for (int num2 = 0; num2 < _objects.Count; num2++)
		{
			GameObject gameObject = _objects[num2];
			Transform transform = gameObject.transform;
			transform.localRotation = Quaternion.identity;
			transform.localPosition = new Vector3(0f, num, 0f);
			num += m_heightOffset;
		}
	}

	public void RefreshStackCollider(ref BoxCollider _collider, int _size)
	{
		float num = (float)_size * m_heightOffset;
		_collider.size = _collider.size.WithY(num);
		_collider.center = _collider.center.WithY(0.5f * num);
	}

	public Transform GetAttachPoint(GameObject gameObject)
	{
		return m_AttachPoint;
	}

	public bool HasClientSidePrediction()
	{
		return false;
	}
}
