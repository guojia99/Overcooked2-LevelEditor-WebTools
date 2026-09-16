using UnityEngine;

[RequireComponent(typeof(Teleportal))]
[RequireComponent(typeof(StaticGridLocation))]
public class TeleportalConveyenceReceiver : MonoBehaviour
{
	[SerializeField]
	[AssignChild("TeleportPoint", Editorbility.NonEditable)]
	public Transform m_attachPoint;
}
