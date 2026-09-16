using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientLocalAvatarSynchroniser : ClientWorldObjectSynchroniser
{
	public override void StartSynchronising(Component synchronisedObject)
	{
		m_bHasEverReceived = true;
	}

	public override EntityType GetEntityType()
	{
		return EntityType.WorldMapVanAvatar;
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
	}

	public override void ApplyServerUpdate(Serialisable serialisable)
	{
	}

	public override void UpdateSynchronising()
	{
	}

	public override void Pause()
	{
	}

	public override void Resume()
	{
	}
}
