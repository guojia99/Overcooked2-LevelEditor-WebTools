using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientHeatedStationGUI : ClientSynchroniserBase
{
	private HeatedStationGUI m_heatedStationGUI;

	private ClientHeatedStation m_heatedStation;

	private HeatValueUIController m_uiInstance;

	private float m_heatValue;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_heatedStationGUI = (HeatedStationGUI)synchronisedObject;
		m_heatedStation = base.gameObject.RequireComponent<ClientHeatedStation>();
		m_heatValue = m_heatedStation.HeatValue;
		OnHeatValueChanged(m_heatValue);
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		if (m_heatedStation != null)
		{
			OnHeatValueChanged(m_heatedStation.HeatValue);
		}
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		if (m_uiInstance != null)
		{
			m_uiInstance.gameObject.SetActive(false);
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (m_uiInstance != null)
		{
			Object.Destroy(m_uiInstance.gameObject);
			m_uiInstance = null;
		}
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
		if (m_heatedStation.HeatValue != m_heatValue)
		{
			m_heatValue = m_heatedStation.HeatValue;
			OnHeatValueChanged(m_heatValue);
		}
	}

	private void OnHeatValueChanged(float _heatValue)
	{
		if (!base.enabled || !base.gameObject.activeInHierarchy)
		{
			return;
		}
		if (_heatValue > 0f || m_heatedStationGUI.m_displayWhenCold)
		{
			if (m_uiInstance == null)
			{
				GameObject obj = GameUtils.InstantiateHoverIconUIController(m_heatedStationGUI.m_heatUIPrefab.gameObject, NetworkUtils.FindVisualRoot(base.gameObject), "HoverIconCanvas", m_heatedStationGUI.m_Offset);
				m_uiInstance = obj.RequireComponent<HeatValueUIController>();
			}
			else
			{
				m_uiInstance.gameObject.SetActive(true);
			}
		}
		else if (m_uiInstance != null)
		{
			m_uiInstance.gameObject.SetActive(false);
		}
		if (m_uiInstance != null)
		{
			m_uiInstance.SetProgress(_heatValue);
		}
	}
}
