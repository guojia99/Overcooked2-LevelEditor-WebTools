using System.Collections;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerUtensilRespawnBehaviour : ServerSynchroniserBase, IRespawnBehaviour, ISurfacePlacementNotified
{
	protected UtensilRespawnBehaviour m_utensilRespawnBehaviour;

	private RespawnMessage m_ServerData = new RespawnMessage();

	protected ServerAttachStation m_IdealSpawnLocation;

	private bool m_isRespawning;

	private ServerAttachStation[] m_allRespawnStations;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_utensilRespawnBehaviour = (UtensilRespawnBehaviour)synchronisedObject;
	}

	public override EntityType GetEntityType()
	{
		return EntityType.RespawnBehaviour;
	}

	public IEnumerator RespawnCoroutine(ServerRespawnCollider _collider)
	{
		if (m_isRespawning)
		{
			yield break;
		}
		m_isRespawning = true;
		ServerPhysicalAttachment attachment = base.gameObject.RequestComponent<ServerPhysicalAttachment>();
		ReleaseFromAttached(attachment);
		if ((bool)attachment)
		{
			attachment.ManualDisable(true);
		}
		m_ServerData.m_RespawnType = RespawnCollider.RespawnType.FallDeath;
		m_ServerData.m_Phase = RespawnMessage.Phase.Begin;
		SendServerEvent(m_ServerData);
		ServerContentsDisposalBehaviour disposal = base.gameObject.RequestComponent<ServerContentsDisposalBehaviour>();
		if (disposal != null)
		{
			disposal.AddToDisposer(null);
		}
		base.gameObject.SetActive(false);
		IEnumerator wait = CoroutineUtils.TimerRoutine(m_utensilRespawnBehaviour.m_respawnTime, base.gameObject.layer);
		while (wait.MoveNext())
		{
			yield return null;
		}
		ServerAttachStation freeStation = null;
		while (freeStation == null)
		{
			yield return null;
			freeStation = GetFreeAttachStation();
		}
		if ((bool)attachment)
		{
			attachment.ManualEnable();
		}
		AddItemToStation(freeStation);
		if (!(this == null) && !(base.gameObject == null))
		{
			Collider collider = base.gameObject.GetComponent<Collider>();
			if (collider != null)
			{
				collider.enabled = true;
			}
			m_ServerData.m_Phase = RespawnMessage.Phase.End;
			m_ServerData.m_RespawnPosition = freeStation.GetAttachPoint(base.gameObject).position;
			SendServerEvent(m_ServerData);
			m_isRespawning = false;
		}
	}

	public void OnSurfacePlacement(ServerAttachStation _station)
	{
		if (m_IdealSpawnLocation == null)
		{
			m_IdealSpawnLocation = _station;
		}
	}

	public void OnSurfaceDeplacement(ServerAttachStation _station)
	{
	}

	public void SetIdealRespawnLocation(ServerAttachStation _station)
	{
		m_IdealSpawnLocation = _station;
	}

	public ServerAttachStation GetIdealRespawnLocation()
	{
		return m_IdealSpawnLocation;
	}

	public virtual float GetStationRespawnPriority(ServerAttachStation _station)
	{
		return GetRespawnDistance(_station.transform.position);
	}

	protected float GetRespawnDistance(Vector3 _position)
	{
		Vector3 vector = ((!(m_IdealSpawnLocation != null)) ? base.transform.position : m_IdealSpawnLocation.transform.position);
		return (_position - vector).magnitude;
	}

	protected bool IsInLevelBounds(ServerAttachStation _station, LevelConfigBase _levelConfig = null)
	{
		if (_levelConfig == null)
		{
			_levelConfig = GameUtils.GetLevelConfig();
		}
		return !_levelConfig.m_enableRespawnBounds || LevelBounds.ActiveBoundsContain(_station.transform.position);
	}

	protected virtual bool CanRespawnOnStation(ServerAttachStation _attachStation)
	{
		if (_attachStation.CompareTag("CookingStation") || _attachStation.CompareTag("PlateReturn") || _attachStation.CompareTag("PlateStation") || _attachStation.gameObject.RequestComponent<RubbishBin>() != null || _attachStation.gameObject.RequestComponent<ConveyorStation>() != null || _attachStation.gameObject.RequestComponent<WashingStation>() != null)
		{
			return false;
		}
		return _attachStation.CanAttachToSelf(base.gameObject);
	}

	protected virtual ServerAttachStation GetFreeAttachStation()
	{
		ServerAttachStation result = null;
		float num = float.PositiveInfinity;
		LevelConfigBase levelConfig = GameUtils.GetLevelConfig();
		if (m_allRespawnStations == null)
		{
			m_allRespawnStations = GetRespawnStations();
		}
		for (int i = 0; i < m_allRespawnStations.Length; i++)
		{
			ServerAttachStation serverAttachStation = m_allRespawnStations[i];
			if (serverAttachStation.enabled && serverAttachStation.gameObject.activeInHierarchy && IsInLevelBounds(serverAttachStation, levelConfig) && CanRespawnOnStation(serverAttachStation))
			{
				float stationRespawnPriority = GetStationRespawnPriority(serverAttachStation);
				if (stationRespawnPriority < num)
				{
					result = serverAttachStation;
					num = stationRespawnPriority;
				}
			}
		}
		return result;
	}

	protected virtual ServerAttachStation[] GetRespawnStations()
	{
		return Object.FindObjectsOfType<ServerAttachStation>();
	}

	private static void ReleaseFromAttached(ServerPhysicalAttachment _attachment)
	{
		if (!_attachment.IsAttached())
		{
			return;
		}
		ServerHandlePickupReferral serverHandlePickupReferral = _attachment.gameObject.RequestComponent<ServerHandlePickupReferral>();
		if (!(serverHandlePickupReferral != null))
		{
			return;
		}
		IHandlePickup handlePickupReferree = serverHandlePickupReferral.GetHandlePickupReferree();
		if (handlePickupReferree == null)
		{
			return;
		}
		if (handlePickupReferree as ServerAttachStation != null)
		{
			ServerAttachStation serverAttachStation = handlePickupReferree as ServerAttachStation;
			if (serverAttachStation.HasItem())
			{
				serverAttachStation.TakeItem();
			}
		}
		else if (handlePickupReferree is ServerPlayerAttachmentCarrier.BlockPickup)
		{
			ServerPlayerAttachmentCarrier.BlockPickup blockPickup = handlePickupReferree as ServerPlayerAttachmentCarrier.BlockPickup;
			blockPickup.ForceDetach();
		}
	}

	protected virtual void AddItemToStation(ServerAttachStation _station)
	{
		_station.AddItem(base.gameObject, -_station.transform.forward.XZ());
	}
}
