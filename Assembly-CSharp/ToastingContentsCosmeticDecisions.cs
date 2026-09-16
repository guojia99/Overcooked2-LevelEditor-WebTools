using UnityEngine;

public class ToastingContentsCosmeticDecisions : OverlapModelsMealDecisions
{
	[SerializeField]
	private OrderToPrefabLookup m_cookedPrefabLookup;

	[SerializeField]
	private GameObject m_burntPrefab;

	[SerializeField]
	private float m_contentsOffset = 0.666f;

	protected override void Start()
	{
		base.Start();
		if (m_cookedPrefabLookup != null)
		{
			m_cookedPrefabLookup.CacheAssembledOrderNodes();
		}
		m_container.transform.localPosition = new Vector3(m_contentsOffset, 0f, 0f);
		m_container.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
	}

	protected override IClientOrderDefinition FindOrderDefinition()
	{
		return base.gameObject.RequestInterfaceUpwardsRecursive<IClientOrderDefinition>();
	}

	protected override void CreateRenderChildren(AssembledDefinitionNode _contents)
	{
		base.CreateRenderChildren(_contents);
		if (_contents.Simpilfy() != AssembledDefinitionNode.NullNode && m_container.transform.childCount == 0 && m_burntPrefab != null)
		{
			m_burntPrefab.InstantiateOnParent(m_container.transform);
		}
	}
}
