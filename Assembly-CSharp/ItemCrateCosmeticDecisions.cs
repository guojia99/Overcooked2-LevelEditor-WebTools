using UnityEngine;

[RequireComponent(typeof(PickupItemSpawner))]
public class ItemCrateCosmeticDecisions : MonoBehaviour
{
	[SerializeField]
	public Transform m_cosmeticItemAttachpoint;

	[SerializeField]
	public string m_crateLidMeshName = "CrateLid_mesh";

	[SerializeField]
	public int m_materialNumber = 1;

	[SerializeField]
	public Vector2 m_uvScale = new Vector2(2f, 2f);
}
