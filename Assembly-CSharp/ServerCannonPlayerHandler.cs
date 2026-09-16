using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerCannonPlayerHandler : ServerSynchroniserBase, IServerCannonHandler
{
	private ServerCannon m_cannon;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_cannon = synchronisedObject.gameObject.RequireComponent<ServerCannon>();
	}

	public bool CanHandle(GameObject _obj)
	{
		if (_obj == null)
		{
			return false;
		}
		return _obj.GetComponent<PlayerControls>() != null;
	}

	public void ExitCannonRoutine(GameObject _obj)
	{
		_obj.GetComponent<Rigidbody>().isKinematic = false;
		GroundCast groundCast = _obj.RequestComponent<GroundCast>();
		if (groundCast != null)
		{
			groundCast.ForceUpdateNow();
		}
		ServerWorldObjectSynchroniser serverWorldObjectSynchroniser = _obj.RequestComponent<ServerWorldObjectSynchroniser>();
		if (serverWorldObjectSynchroniser != null)
		{
			serverWorldObjectSynchroniser.ResumeAllClients(false);
		}
	}

	public void Load(GameObject _obj)
	{
	}

	public void Unload(GameObject _obj)
	{
	}

	public bool IsFlying()
	{
		return m_cannon.IsFlying();
	}
}
