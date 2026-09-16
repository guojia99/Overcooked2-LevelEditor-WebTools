using UnityEngine;

public class Teleportal : MonoBehaviour
{
	[SerializeField]
	[AssignChild("TeleportPoint", Editorbility.NonEditable)]
	public Transform m_teleportPoint;

	[SerializeField]
	[Range(0f, 180f)]
	public float m_teleportArc = 90f;

	[SerializeField]
	public GameObject m_exitPortal;

	[SerializeField]
	public float m_receiveDelay;

	[SerializeField]
	public float m_cooldownTime;

	[SerializeField]
	public bool m_allowImmediateReteleport;
}
