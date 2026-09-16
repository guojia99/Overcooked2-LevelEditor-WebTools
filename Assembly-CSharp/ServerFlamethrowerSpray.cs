using System.Collections.Generic;
using UnityEngine;

public class ServerFlamethrowerSpray : ServerSprayingUtensil
{
	private class StationSmouldering
	{
		public CookingStation Cooker;

		public GameObject Smoulder;

		public float LifeTime;
	}

	protected FlamethrowerSpray m_FlamethrowerSpray;

	private static Dictionary<AttachStation, StationSmouldering> m_smoulderLookup = new Dictionary<AttachStation, StationSmouldering>();

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_FlamethrowerSpray = (FlamethrowerSpray)synchronisedObject;
	}

	public override void UpdateSynchronising()
	{
		if (IsSpraying())
		{
			IEnumerable<ServerCookingHandler> cookingHandlers = ServerCookingHandler.GetCookingHandlers();
			foreach (ServerCookingHandler item in cookingHandlers)
			{
				if (IsInSpray(item.transform))
				{
					Cook(item);
				}
			}
		}
		Generic<bool, KeyValuePair<AttachStation, StationSmouldering>> shouldRemove = delegate(KeyValuePair<AttachStation, StationSmouldering> _kvPair)
		{
			_kvPair.Value.LifeTime -= TimeManager.GetDeltaTime(base.gameObject);
			if (_kvPair.Value.LifeTime < 0f)
			{
				EndSmoulder(_kvPair.Value);
				return true;
			}
			return false;
		};
		m_smoulderLookup.RemoveAll(shouldRemove);
	}

	private void EndSmoulder(StationSmouldering _smoulder)
	{
		Object.Destroy(_smoulder.Smoulder);
		Object.Destroy(_smoulder.Cooker);
	}

	private void Cook(ServerCookingHandler _handler)
	{
		if (!_handler.Cook(m_FlamethrowerSpray.m_cookingRate * TimeManager.GetDeltaTime(base.gameObject)))
		{
			return;
		}
		AttachStation attachStation = _handler.gameObject.RequestComponentUpwardsRecursive<AttachStation>();
		if (attachStation != null)
		{
			if (attachStation.gameObject.RequestComponent<CookingStation>() == null)
			{
				StationSmouldering stationSmouldering = new StationSmouldering();
				stationSmouldering.LifeTime = m_FlamethrowerSpray.m_smoulderTime;
				stationSmouldering.Cooker = attachStation.gameObject.AddComponent<CookingStation>();
				stationSmouldering.Cooker.m_stationType = CookingStationType.Flamethrower;
				stationSmouldering.Cooker.m_attachRestrictions = false;
				stationSmouldering.Smoulder = m_FlamethrowerSpray.m_smoulderEffect.InstantiateOnParent(attachStation.GetAttachPoint(_handler.gameObject));
				m_smoulderLookup[attachStation] = stationSmouldering;
			}
			else if (m_smoulderLookup.ContainsKey(attachStation))
			{
				m_smoulderLookup[attachStation].LifeTime = m_FlamethrowerSpray.m_smoulderTime;
			}
		}
		if (_handler.IsBurning())
		{
			ServerFlammable serverFlammable = _handler.gameObject.RequestComponentUpwardsRecursive<ServerFlammable>();
			if (serverFlammable != null)
			{
				serverFlammable.Ignite();
			}
		}
	}
}
