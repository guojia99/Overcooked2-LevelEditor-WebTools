using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class OrderToPrefabLookup : ScriptableObject
{
	[Serializable]
	public class ContentPrefabLookup
	{
		public OrderDefinitionNode m_content;

		public GameObject m_prefab;

		public int m_amountAllowed = 1;

		public OrderDefinitionNode[] m_restrictedContent = new OrderDefinitionNode[0];

		[NonSerialized]
		public AssembledDefinitionNode m_simplifiedAssembledContent;
	}

	[SerializeField]
	private ContentPrefabLookup[] m_lookupArray = new ContentPrefabLookup[0];

	[NonSerialized]
	private bool m_cachedAssembledOrderNodes;

	public void CacheAssembledOrderNodes()
	{
		if (!m_cachedAssembledOrderNodes)
		{
			m_cachedAssembledOrderNodes = true;
			for (int i = 0; i < m_lookupArray.Length; i++)
			{
				m_lookupArray[i].m_simplifiedAssembledContent = m_lookupArray[i].m_content.Simpilfy();
			}
		}
	}

	public GameObject GetPrefabForNode(AssembledDefinitionNode _node)
	{
		if (!m_cachedAssembledOrderNodes)
		{
			CacheAssembledOrderNodes();
		}
		AssembledDefinitionNode simpleNode = _node.Simpilfy();
		for (int i = 0; i < m_lookupArray.Length; i++)
		{
			if (AssembledDefinitionNode.MatchingAlreadySimple(simpleNode, m_lookupArray[i].m_simplifiedAssembledContent))
			{
				return m_lookupArray[i].m_prefab;
			}
		}
		return null;
	}

	public List<OrderContentRestriction> GetContentRestrictions()
	{
		List<OrderContentRestriction> list = new List<OrderContentRestriction>();
		for (int i = 0; i < m_lookupArray.Length; i++)
		{
			OrderContentRestriction orderContentRestriction = new OrderContentRestriction();
			orderContentRestriction.m_content = m_lookupArray[i].m_content;
			orderContentRestriction.m_amountAllowed = m_lookupArray[i].m_amountAllowed;
			orderContentRestriction.m_restrictedContent = m_lookupArray[i].m_restrictedContent;
			list.Add(orderContentRestriction);
		}
		return list;
	}
}
