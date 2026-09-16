using System.Collections.Generic;
using UnityEngine;

public class ServerBellowsSpray : ServerSprayingUtensil, IWindSource, IHeatTransferBehaviour
{
	private BellowsSpray m_bellowsSpray;

	private List<WindAccumulator> m_activeWindReceivers = new List<WindAccumulator>();

	private ServerUsableItem m_interactable;

	private float m_knockbackTimer;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_bellowsSpray = (BellowsSpray)synchronisedObject;
		m_interactable = base.gameObject.RequestComponent<ServerUsableItem>();
	}

	protected override void StartSpray()
	{
		base.StartSpray();
		List<ServerHeatedStation> allHeatedStations = ServerHeatedStation.GetAllHeatedStations();
		for (int i = 0; i < allHeatedStations.Count; i++)
		{
			ServerHeatedStation serverHeatedStation = allHeatedStations[i];
			if (IsInSpray(serverHeatedStation.transform))
			{
				ServerCookingStation serverCookingStation = serverHeatedStation.gameObject.RequestComponent<ServerCookingStation>();
				if (serverCookingStation != null && serverCookingStation.StationType == CookingStationType.Barbeque && base.Carrier != null)
				{
					ServerMessenger.Achievement(base.Carrier, 102);
				}
				allHeatedStations[i].ExternalHeatTransfer(this);
			}
		}
		m_knockbackTimer = m_bellowsSpray.m_knockback.Duration;
		if (m_interactable != null)
		{
			m_interactable.SetInteractionSuppressed(true);
		}
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
		if (m_knockbackTimer > 0f)
		{
			m_knockbackTimer -= TimeManager.GetDeltaTime(base.gameObject);
			if (m_knockbackTimer <= 0f)
			{
				StopSpray();
				if (m_interactable != null)
				{
					m_interactable.SetInteractionSuppressed(false);
				}
			}
		}
		if (IsSpraying())
		{
			List<WindAccumulator> allWindReceivers = WindAccumulator.GetAllWindReceivers();
			for (int i = 0; i < allWindReceivers.Count; i++)
			{
				WindAccumulator windAccumulator = allWindReceivers[i];
				int num = m_activeWindReceivers.IndexOf(windAccumulator);
				bool flag = IsInSpray(windAccumulator.transform);
				bool flag2 = num >= 0;
				if (!flag2 && flag)
				{
					m_activeWindReceivers.Add(windAccumulator);
					allWindReceivers[i].AddWindSource(this);
				}
				else if (flag2 && !flag)
				{
					m_activeWindReceivers.RemoveAt(num);
					allWindReceivers[i].RemoveWindSource(this);
				}
			}
		}
		else if (m_activeWindReceivers.Count > 0)
		{
			for (int j = 0; j < m_activeWindReceivers.Count; j++)
			{
				m_activeWindReceivers[j].RemoveWindSource(this);
			}
			m_activeWindReceivers.Clear();
		}
	}

	public Vector3 GetVelocity()
	{
		return base.transform.forward * m_bellowsSpray.m_knockback.Force;
	}

	public bool CanTransferToContainer(IHeatContainer _container)
	{
		return true;
	}

	public void TransferToContainer(ICarrierPlacement _carrier, IHeatContainer _container)
	{
		_container.IncreaseHeat(m_bellowsSpray.m_heatIncrease);
	}
}
