using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerSlipCollider : ServerSynchroniserBase
{
	private SlipCollider m_slipCollider;

	private List<GameObject> m_slipped = new List<GameObject>();

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_slipCollider = (SlipCollider)synchronisedObject;
	}

	private void Slip(GameObject _object)
	{
		ServerPlayerSlipBehaviour serverPlayerSlipBehaviour = _object.RequestComponent<ServerPlayerSlipBehaviour>();
		if (serverPlayerSlipBehaviour != null)
		{
			serverPlayerSlipBehaviour.Slip(this);
		}
		if (m_slipCollider.m_deleteOnSlip)
		{
			Object.Destroy(base.gameObject);
		}
	}

	public void ObjectAdded(GameObject _object)
	{
		if ((m_slipCollider.m_slipFilter.value & (1 << _object.layer)) != 0 && !m_slipped.Contains(_object))
		{
			Slip(_object);
			m_slipped.Add(_object);
		}
	}

	public void ObjectRemoved(GameObject _object)
	{
		if ((m_slipCollider.m_slipFilter.value & (1 << _object.layer)) != 0)
		{
			m_slipped.Remove(_object);
		}
	}

	private void OnCollisionEnter(Collision collision)
	{
		ObjectAdded(collision.gameObject);
	}

	private void OnTriggerEnter(Collider collider)
	{
		ObjectAdded(collider.gameObject);
	}

	private void OnCollisionExit(Collision collision)
	{
		ObjectRemoved(collision.gameObject);
	}

	private void OnTriggerExit(Collider collider)
	{
		ObjectRemoved(collider.gameObject);
	}
}
