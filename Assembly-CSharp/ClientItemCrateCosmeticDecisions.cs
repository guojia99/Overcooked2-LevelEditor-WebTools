using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientItemCrateCosmeticDecisions : ClientSynchroniserBase
{
	private ItemCrateCosmeticDecisions m_itemCrateCosmeticDecisions;

	private static readonly int m_iOpen = Animator.StringToHash("Open");

	private Animator m_animator;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_itemCrateCosmeticDecisions = (ItemCrateCosmeticDecisions)synchronisedObject;
		ClientPickupItemSpawner component = base.gameObject.GetComponent<ClientPickupItemSpawner>();
		GameObject itemPrefab = component.GetItemPrefab();
		WorkableItem workableItem = itemPrefab.RequestComponent<WorkableItem>();
		ISpawnableItem spawnableItem;
		if (workableItem != null)
		{
			GameObject nextPrefab = workableItem.GetNextPrefab();
			spawnableItem = nextPrefab.RequireInterface<ISpawnableItem>();
		}
		else
		{
			spawnableItem = itemPrefab.RequireInterface<ISpawnableItem>();
		}
		SubTexture2D subTexture = spawnableItem.GetSubTexture();
		Transform transform = base.transform.FindChildRecursive(m_itemCrateCosmeticDecisions.m_crateLidMeshName);
		Renderer component2 = transform.GetComponent<SkinnedMeshRenderer>();
		if (component2 == null)
		{
			component2 = transform.GetComponent<MeshRenderer>();
		}
		Material material = component2.materials[m_itemCrateCosmeticDecisions.m_materialNumber];
		material.mainTexture = subTexture.m_atlasTexture;
		float num = subTexture.m_atlasTexture.width;
		float num2 = subTexture.m_atlasTexture.height;
		float x = subTexture.m_subRect.x / num;
		float y = 1f / m_itemCrateCosmeticDecisions.m_uvScale.y - subTexture.m_subRect.y / num2;
		material.mainTextureOffset = new Vector2(x, y);
		float x2 = m_itemCrateCosmeticDecisions.m_uvScale.x * subTexture.m_subRect.width / num;
		float num3 = m_itemCrateCosmeticDecisions.m_uvScale.y * subTexture.m_subRect.height / num2;
		material.mainTextureScale = new Vector2(x2, 0f - num3);
		m_animator = base.gameObject.RequestComponentRecursive<Animator>();
	}

	public void OnPickupItem()
	{
		m_animator.SetTrigger(m_iOpen);
	}
}
