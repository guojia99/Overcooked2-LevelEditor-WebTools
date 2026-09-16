using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientStack : ClientSynchroniserBase
{
	private Stack m_stack;

	private List<GameObject> m_stackItems = new List<GameObject>();

	private BoxCollider m_boxCollider;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_stack = (Stack)synchronisedObject;
		m_boxCollider = base.gameObject.GetComponent<BoxCollider>();
		for (int i = 0; i < base.transform.childCount; i++)
		{
			if (base.transform.GetChild(i).gameObject.CompareTag(m_stack.m_itemTag))
			{
				m_stackItems.Add(base.transform.GetChild(i).gameObject);
			}
		}
		m_stack.RefreshStackTransforms(ref m_stackItems);
		m_stack.RefreshStackCollider(ref m_boxCollider, m_stackItems.Count);
	}

	public void AddToStack(GameObject _item)
	{
		if (!m_stackItems.Contains(_item))
		{
			IClientAttachment clientAttachment = _item.RequestInterface<IClientAttachment>();
			if (clientAttachment == null)
			{
				_item.transform.SetParent(m_stack.GetAttachPoint(_item));
			}
			m_stackItems.Add(_item);
			m_stack.RefreshStackTransforms(ref m_stackItems);
			m_stack.RefreshStackCollider(ref m_boxCollider, m_stackItems.Count);
		}
	}

	public GameObject RemoveFromStack()
	{
		if (m_stackItems.Count > 0)
		{
			GameObject gameObject = m_stackItems[m_stackItems.Count - 1];
			m_stackItems.Remove(gameObject);
			IClientAttachment clientAttachment = gameObject.RequestInterface<IClientAttachment>();
			if (clientAttachment == null)
			{
				gameObject.transform.SetParent(null);
			}
			m_stack.RefreshStackTransforms(ref m_stackItems);
			m_stack.RefreshStackCollider(ref m_boxCollider, m_stackItems.Count);
			return gameObject;
		}
		return null;
	}

	public int GetSize()
	{
		return m_stackItems.Count;
	}

	public GameObject InspectTopOfStack()
	{
		if (m_stackItems.Count > 0)
		{
			return m_stackItems[m_stackItems.Count - 1];
		}
		return null;
	}
}
