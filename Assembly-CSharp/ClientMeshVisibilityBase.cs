using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public abstract class ClientMeshVisibilityBase<StateEnum> : ClientSynchroniserBase where StateEnum : struct, IConvertible
{
	public MeshVisibilityBase<StateEnum> m_visibility;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_visibility = (MeshVisibilityBase<StateEnum>)synchronisedObject;
	}

	protected void Setup(StateEnum _state)
	{
		if (m_visibility != null)
		{
			m_visibility.Setup(_state);
		}
	}

	protected void SetState(StateEnum _state)
	{
		if (m_visibility != null)
		{
			m_visibility.Setup(_state);
		}
	}
}
