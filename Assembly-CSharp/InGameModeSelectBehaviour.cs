using GameModes;
using Team17.Online;
using UnityEngine;
using UnityEngine.UI;

public class InGameModeSelectBehaviour : InGameMenuBehaviour
{
	[SerializeField]
	[AssignResource("GameModeUIData", Editorbility.Editable)]
	private GameModeUIData m_gameModeUIData;

	[SerializeField]
	private RectTransform m_elementParent;

	[SerializeField]
	[AssignResource("InGameModeSelectElement", Editorbility.Editable)]
	private GameObject m_elementPrefab;

	private TimeManager m_timeManager;

	private TimeManager.PauseLayer[] m_pauseLayers;

	protected override void SingleTimeInitialize()
	{
		base.SingleTimeInitialize();
		for (int i = 0; i < m_gameModeUIData.m_gameModes.Length; i++)
		{
			GameObject obj = GameUtils.InstantiateUIController(m_elementPrefab, m_elementParent);
			GameModeDialogueElementUIController gameModeDialogueElementUIController = obj.RequestComponent<GameModeDialogueElementUIController>();
			gameModeDialogueElementUIController.SetData(this, (Kind)i, m_gameModeUIData.m_gameModes[i]);
			if (i == 0)
			{
				Selectable selectable = obj.RequireComponent<Selectable>();
				Navigation borderSelectables = m_BorderSelectables;
				borderSelectables.selectOnDown = selectable;
				borderSelectables.selectOnUp = selectable;
				borderSelectables.selectOnLeft = selectable;
				borderSelectables.selectOnRight = selectable;
				m_BorderSelectables = borderSelectables;
			}
		}
		m_timeManager = GameUtils.RequestManager<TimeManager>();
	}

	public override bool Show(GamepadUser currentGamer, BaseMenuBehaviour parent, GameObject invoker, bool hideInvoker = true)
	{
		if (!base.Show(currentGamer, parent, invoker, hideInvoker))
		{
			return false;
		}
		m_pauseLayers = GetLayersToPause();
		for (int i = 0; i < m_pauseLayers.Length; i++)
		{
			m_timeManager.SetPaused(m_pauseLayers[i], true, this);
		}
		return true;
	}

	public override bool Hide(bool restoreInvokerState = true, bool isTabSwitch = false)
	{
		if (!base.Hide(restoreInvokerState, isTabSwitch))
		{
			return false;
		}
		if (m_pauseLayers != null && m_pauseLayers.Length > 0)
		{
			for (int i = 0; i < m_pauseLayers.Length; i++)
			{
				m_timeManager.SetPaused(m_pauseLayers[i], false, this);
			}
			m_pauseLayers = null;
		}
		return true;
	}

	private TimeManager.PauseLayer[] GetLayersToPause()
	{
		return (!ConnectionStatus.IsInSession() || !UserSystemUtils.AnyRemoteUsers()) ? new TimeManager.PauseLayer[1] : new TimeManager.PauseLayer[1] { TimeManager.PauseLayer.Network };
	}
}
