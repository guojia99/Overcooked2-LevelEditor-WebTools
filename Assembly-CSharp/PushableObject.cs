using UnityEngine;

[RequireComponent(typeof(Interactable))]
public class PushableObject : SessionInteractable
{
	[SerializeField]
	public GameObject m_CentrePoint;

	[SerializeField]
	public bool m_UseAttachPoints = true;

	[SerializeField]
	public GameObject[] m_AttachPoints = new GameObject[0];

	[SerializeField]
	[HideInInspectorTest("m_UseAttachPoints", false)]
	public float m_idealPlayerDistance = 1.4f;

	[SerializeField]
	[HideInInspectorTest("m_UseAttachPoints", false)]
	public float m_lerpPlayerSpeed = 6f;

	[SerializeField]
	[AssignChildRecursive("PlayerCollisionEmulator", Editorbility.Editable)]
	public Collider m_fakePlayerCollider;

	[SerializeField]
	public bool m_playDraggingAudio;

	[SerializeField]
	[HideInInspectorTest("m_playDraggingAudio", true)]
	public GameLoopingAudioTag m_draggingAudioTag = GameLoopingAudioTag.COUNT;

	private void Awake()
	{
		Rigidbody rigidbody = base.gameObject.RequireComponent<Rigidbody>();
		Rigidbody[] array = base.gameObject.RequestComponentsRecursive<Rigidbody>();
		foreach (Rigidbody rigidbody2 in array)
		{
			if (rigidbody2.gameObject != base.gameObject)
			{
				Object.Destroy(rigidbody2);
			}
		}
		Collider[] array2 = base.gameObject.RequestComponentsRecursive<Collider>();
		for (int j = 0; j < array2.Length; j++)
		{
			Physics.IgnoreCollision(m_fakePlayerCollider, array2[j], true);
		}
	}

	public Transform GetAttachPoint(Transform child)
	{
		Transform result = base.transform;
		if (m_UseAttachPoints)
		{
			float num = float.MaxValue;
			for (int i = 0; i < m_AttachPoints.Length; i++)
			{
				IParentable parentable = m_AttachPoints[i].RequestInterface<IParentable>();
				if (parentable != null)
				{
					Transform attachPoint = parentable.GetAttachPoint(child.gameObject);
					float sqrMagnitude = (attachPoint.position - child.position).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						result = attachPoint;
						num = sqrMagnitude;
					}
				}
			}
		}
		else if (m_CentrePoint != null)
		{
			result = m_CentrePoint.transform;
		}
		return result;
	}

	public bool IsAttached(Transform child)
	{
		if (m_UseAttachPoints)
		{
			for (int i = 0; i < m_AttachPoints.Length; i++)
			{
				IParentable parentable = m_AttachPoints[i].RequestInterface<IParentable>();
				if (parentable != null && child.parent == parentable.GetAttachPoint(child.gameObject))
				{
					return true;
				}
			}
		}
		else if (m_CentrePoint != null)
		{
			return child.parent == m_CentrePoint.transform;
		}
		return false;
	}

	private void OnDestroy()
	{
		if (m_fakePlayerCollider != null)
		{
			Object.Destroy(m_fakePlayerCollider);
		}
	}
}
