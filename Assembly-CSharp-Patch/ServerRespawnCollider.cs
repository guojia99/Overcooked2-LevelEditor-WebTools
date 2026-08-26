using LevelEditor;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerRespawnCollider : ServerSynchroniserBase
{
	private RespawnCollider m_RespawnCollider;

	private RespawnColliderMessage m_data = new RespawnColliderMessage();

	public RespawnCollider.RespawnType Type
	{
		get
		{
			return m_RespawnCollider.m_respawnType;
		}
	}

	public override EntityType GetEntityType()
	{
		return EntityType.RespawnCollider;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_RespawnCollider = (RespawnCollider)synchronisedObject;
	}

	private void SendRespawnMessage(GameObject _gameObject)
	{
		m_data.Initialise(_gameObject);
		SendServerEvent(m_data);
	}

	public void ObjectAdded(GameObject _gameObject)
	{
		if (_gameObject == null || m_RespawnCollider == null)
			return;
		if (_gameObject.GetComponent<PlayerControls>() != null)
			LayoutRuntimePushableVoidFall.DetachPlayerFromVoidFallPots(_gameObject);
		if (LayoutRuntimePushableVoidFall.ShouldIgnoreKillPlane(_gameObject))
			return;
		if ((m_RespawnCollider.m_respawnFilter.value & (1 << _gameObject.layer)) == 0)
			return;
		if (m_RespawnCollider.m_onlyRespawnables && _gameObject.RequestInterfaceRecursive<IRespawnBehaviour>() == null)
			return;
		if (EntitySerialisationRegistry.GetEntry(_gameObject) == null)
			return;
		SendRespawnMessage(_gameObject);
		ServerPlayerRespawnManager.KillOrRespawn(_gameObject, this);
		if (m_RespawnCollider.m_onRespawnTrigger != string.Empty)
		{
			base.gameObject.SendTrigger(m_RespawnCollider.m_onRespawnTrigger);
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
}
