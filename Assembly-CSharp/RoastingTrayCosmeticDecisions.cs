using System.Collections.Generic;
using UnityEngine;

public class RoastingTrayCosmeticDecisions : OverlapModelsMealDecisions
{
	private const int c_panSections = 4;

	[SerializeField]
	private OrderToPrefabLookup m_repositionLookup;

	[SerializeField]
	private GameObject m_burntPrefab;

	[SerializeField]
	private Vector2 m_modelOffsetXZ = Vector2.zero;

	private List<GameObject> m_nodesToReposition = new List<GameObject>();

	protected override IClientOrderDefinition FindOrderDefinition()
	{
		return base.gameObject.RequestInterfaceUpwardsRecursive<IClientOrderDefinition>();
	}

	protected override void Start()
	{
		base.Start();
		m_container.transform.localPosition = new Vector3(0f, 0f, 0f);
		if (m_repositionLookup != null)
		{
			m_repositionLookup.CacheAssembledOrderNodes();
		}
	}

	protected override void UpdateAppearance(AssembledDefinitionNode _contents)
	{
		base.UpdateAppearance(_contents);
		if (_contents == null)
		{
			return;
		}
		bool flag = _contents is CookedCompositeAssembledNode && (_contents as CookedCompositeAssembledNode).m_progress == CookedCompositeOrderNode.CookingProgress.Burnt;
		bool flag2 = _contents.Simpilfy() != AssembledDefinitionNode.NullNode && m_container.transform.childCount == 0;
		if ((flag || flag2) && m_burntPrefab != null)
		{
			DestroyAllRenderingInstances();
			int nodeCount = _contents.GetNodeCount();
			for (int i = 0; i < 4; i++)
			{
				GameObject gameObject = m_burntPrefab.InstantiateOnParent(m_container.transform);
			}
		}
	}

	protected override void CreateRenderChildren(AssembledDefinitionNode _contents)
	{
		m_nodesToReposition.Clear();
		IEnumerator<AssembledDefinitionNode> enumerator = ShallowIteration(_contents);
		while (enumerator.MoveNext())
		{
			AssembledDefinitionNode current = enumerator.Current;
			GameObject renderPefabForNode = GetRenderPefabForNode(current);
			if (renderPefabForNode != null)
			{
				GameObject item = renderPefabForNode.InstantiateOnParent(m_container.transform);
				if (m_repositionLookup != null && m_repositionLookup.GetPrefabForNode(current) != null)
				{
					m_nodesToReposition.Add(item);
				}
			}
		}
		RepositionRenderChildren(m_nodesToReposition, Mathf.Min(m_nodesToReposition.Count, 4));
	}

	protected override GameObject GetRenderPefabForNode(AssembledDefinitionNode _order)
	{
		if (_order is CookedCompositeAssembledNode)
		{
			CookedCompositeAssembledNode cookedCompositeAssembledNode = _order as CookedCompositeAssembledNode;
			if (cookedCompositeAssembledNode.m_composition.Length == 1)
			{
				return base.GetRenderPefabForNode(cookedCompositeAssembledNode.m_composition[0]);
			}
		}
		return base.GetRenderPefabForNode(_order);
	}

	protected virtual void RepositionRenderChildren(List<GameObject> nodes, int _totalSections)
	{
		if (nodes.Count > 1)
		{
			Quaternion quaternion = Quaternion.Euler(0f, -360f / (float)_totalSections, 0f);
			Vector3 vector = new Vector3(m_modelOffsetXZ.x, 0f, m_modelOffsetXZ.y);
			Quaternion quaternion2 = Quaternion.identity;
			for (int i = 0; i < nodes.Count; i++)
			{
				Transform child = m_container.transform.GetChild(i);
				child.localPosition = vector;
				child.localRotation = quaternion2;
				vector = quaternion * vector;
				quaternion2 = quaternion * quaternion2;
			}
		}
	}
}
