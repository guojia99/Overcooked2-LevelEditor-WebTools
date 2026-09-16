using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerStack : ServerSynchroniserBase
{
	private Stack m_stack;

	private List<GameObject> m_stackItems = new List<GameObject>();

	private BoxCollider m_boxCollider;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_stack = synchronisedObject as Stack;
		m_boxCollider = base.gameObject.GetComponent<BoxCollider>();
		Transform transform = base.transform;
		int childCount = transform.childCount;
		for (int i = 0; i < childCount; i++)
		{
			Transform child = transform.GetChild(i);
			if (child.gameObject.tag == m_stack.m_itemTag)
			{
				m_stackItems.Add(child.gameObject);
			}
		}
		m_stack.RefreshStackTransforms(ref m_stackItems);
		m_stack.RefreshStackCollider(ref m_boxCollider, m_stackItems.Count);
	}

	public void AddToStack(GameObject _item)
	{
		if (!m_stackItems.Contains(_item))
		{
			IAttachment attachment = _item.RequestInterface<IAttachment>();
			if (attachment != null)
			{
				attachment.Attach(m_stack);
			}
			else
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
			IAttachment attachment = gameObject.RequestInterface<IAttachment>();
			if (attachment != null)
			{
				attachment.Detach();
			}
			else
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
