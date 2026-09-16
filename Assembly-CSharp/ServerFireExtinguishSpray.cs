using System.Collections.Generic;
using UnityEngine;

public class ServerFireExtinguishSpray : ServerSprayingUtensil
{
	protected FireExtinguishSpray m_FireExtinguishSpray;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_FireExtinguishSpray = (FireExtinguishSpray)synchronisedObject;
	}

	public override void OnCarryBegun(ICarrier _carrier)
	{
		base.OnCarryBegun(_carrier);
		ServerFlammable serverFlammable = base.Carrier.RequestComponent<ServerFlammable>();
		if (serverFlammable != null)
		{
			serverFlammable.SetCanCatchFire(false);
		}
	}

	public override void OnCarryEnded(ICarrier _carrier)
	{
		if (base.Carrier != null)
		{
			ServerFlammable serverFlammable = base.Carrier.RequestComponent<ServerFlammable>();
			if (serverFlammable != null)
			{
				serverFlammable.SetCanCatchFire(true);
			}
		}
		base.OnCarryEnded(_carrier);
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
		if (!IsSpraying())
		{
			return;
		}
		List<ServerFlammable> list = new List<ServerFlammable>();
		foreach (ServerFlammable item in ServerFlammable.GetAllOnFire())
		{
			if (IsInSpray(item.transform))
			{
				list.Add(item);
			}
		}
		for (int i = 0; i < list.Count; i++)
		{
			ServerFlammable serverFlammable = list[i];
			serverFlammable.FightFire(m_FireExtinguishSpray.m_exinguishTime, TimeManager.GetDeltaTime(base.gameObject), true);
		}
	}
}
