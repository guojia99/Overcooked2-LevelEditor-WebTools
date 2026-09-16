using UnityEngine;

public abstract class BaseTeleportalSender : MonoBehaviour, IParentable
{
	[SerializeField]
	[AssignChild("TeleportPoint", Editorbility.NonEditable)]
	public Transform m_attachPoint;

	public Transform GetAttachPoint(GameObject gameObject)
	{
		return m_attachPoint;
	}

	public bool HasClientSidePrediction()
	{
		return false;
	}
}
