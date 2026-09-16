using System;
using UnityEngine;

public class AssignableOrderDefinition : MonoBehaviour, IClientOrderDefinition, IAssignOrderDefinition
{
	[SerializeField]
	private OrderDefinitionNode m_orderData;

	private AssembledDefinitionNode m_assembledOrderData;

	private OrderCompositionChangedCallback m_orderCompositionChangedCallbacks = delegate
	{
	};

	public void Awake()
	{
		if (m_orderData != null)
		{
			m_assembledOrderData = m_orderData.Simpilfy();
		}
		else
		{
			m_assembledOrderData = AssembledDefinitionNode.NullNode;
		}
	}

	public AssembledDefinitionNode GetOrderComposition()
	{
		return m_assembledOrderData;
	}

	public void RegisterOrderCompositionChangedCallback(OrderCompositionChangedCallback _callback)
	{
		m_orderCompositionChangedCallbacks = (OrderCompositionChangedCallback)Delegate.Combine(m_orderCompositionChangedCallbacks, _callback);
	}

	public void UnregisterOrderCompositionChangedCallback(OrderCompositionChangedCallback _callback)
	{
		m_orderCompositionChangedCallbacks = (OrderCompositionChangedCallback)Delegate.Remove(m_orderCompositionChangedCallbacks, _callback);
	}

	public void SetOrderComposition(AssembledDefinitionNode _data)
	{
		m_assembledOrderData = _data;
		m_orderCompositionChangedCallbacks(m_assembledOrderData);
	}
}
