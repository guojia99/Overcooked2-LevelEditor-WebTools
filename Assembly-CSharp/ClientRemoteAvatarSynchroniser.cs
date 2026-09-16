using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientRemoteAvatarSynchroniser : ClientWorldObjectSynchroniser
{
	private Vector3 m_Velocity = Vector3.zero;

	private Rigidbody m_Rigidbody;

	public override void Awake()
	{
		base.Awake();
		m_Rigidbody = GetComponent<Rigidbody>();
	}

	public override EntityType GetEntityType()
	{
		return EntityType.WorldMapVanAvatar;
	}

	public override void ApplyServerUpdate(Serialisable serialisable)
	{
		AvatarPositionMessage avatarPositionMessage = (AvatarPositionMessage)serialisable;
		HandleNetworkMessage(avatarPositionMessage);
		base.ApplyServerUpdate((Serialisable)avatarPositionMessage.WorldObject);
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		AvatarPositionMessage avatarPositionMessage = (AvatarPositionMessage)serialisable;
		HandleNetworkMessage(avatarPositionMessage);
		base.ApplyServerEvent((Serialisable)avatarPositionMessage.WorldObject);
	}

	protected void HandleNetworkMessage(AvatarPositionMessage message)
	{
		m_Velocity = message.Velocity;
		m_Rigidbody.velocity = m_Velocity;
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
	}
}
