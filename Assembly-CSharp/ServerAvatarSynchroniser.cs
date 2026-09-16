using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerAvatarSynchroniser : ServerWorldObjectSynchroniser
{
	private Rigidbody m_Rigidbody;

	private AvatarPositionMessage m_AvatarData = new AvatarPositionMessage();

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_Rigidbody = GetComponent<Rigidbody>();
		base.StartSynchronising(synchronisedObject);
	}

	public override EntityType GetEntityType()
	{
		return EntityType.WorldMapVanAvatar;
	}

	public override Serialisable GetServerUpdate()
	{
		m_AvatarData.WorldObject = (WorldObjectMessage)base.GetServerUpdate();
		if (m_AvatarData.WorldObject != null)
		{
			m_AvatarData.Velocity = m_Rigidbody.velocity;
			return m_AvatarData;
		}
		return null;
	}

	public override void SendServerEvent(Serialisable message)
	{
		m_AvatarData.WorldObject = (WorldObjectMessage)message;
		if (m_AvatarData.WorldObject != null)
		{
			m_AvatarData.Velocity = m_Rigidbody.velocity;
		}
		base.SendServerEvent(m_AvatarData);
	}
}
