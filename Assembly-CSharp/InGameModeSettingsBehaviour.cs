using System;
using GameModes;
using UnityEngine;
using UnityEngine.UI;

public class InGameModeSettingsBehaviour : InGameMenuBehaviour
{
	[SerializeField]
	[AssignResource("GameModeUIData", Editorbility.Editable)]
	private GameModeUIData m_gameModeUIData;

	[SerializeField]
	private T17ScrollView m_scrollView;

	[SerializeField]
	private RectTransform m_elementParent;

	[SerializeField]
	[AssignResource("InGameModeSettingElement", Editorbility.Editable)]
	private GameObject m_elementPrefab;

	[SerializeField]
	private T17Button m_confirmButton;

	[SerializeField]
	private T17Button m_cancelButton;

	private GameObject[] m_settingListElements = new GameObject[3];

	private Selectable[] m_settingListElementSelectables = new Selectable[3];

	private int m_settingListBufferCount;

	private Selectable[] m_settingListBuffer = new Selectable[3];

	private SessionConfig m_sessionConfig = new SessionConfig();

	protected override void SingleTimeInitialize()
	{
		base.SingleTimeInitialize();
		for (int i = 0; i < m_gameModeUIData.m_gameModeSettings.Length; i++)
		{
			SettingKind settingKind = (SettingKind)i;
			m_settingListElements[i] = GameUtils.InstantiateUIController(m_elementPrefab, m_elementParent);
			m_settingListElementSelectables[i] = m_settingListElements[i].RequireComponent<Selectable>();
			if (i == 0)
			{
				Navigation borderSelectables = m_BorderSelectables;
				borderSelectables.selectOnUp = m_settingListElementSelectables[0];
				borderSelectables.selectOnLeft = m_settingListElementSelectables[0];
				borderSelectables.selectOnRight = m_settingListElementSelectables[0];
				m_BorderSelectables = borderSelectables;
			}
		}
	}

	public override bool Show(GamepadUser currentGamer, BaseMenuBehaviour parent, GameObject invoker, bool hideInvoker = true)
	{
		GameSession gameSession = GameUtils.GetGameSession();
		ModeUIData modeUIData = m_gameModeUIData.m_gameModes[(int)gameSession.GameModeKind];
		if (modeUIData.m_supportedSettings == null || modeUIData.m_supportedSettings.Length < 1)
		{
			return false;
		}
		if (!base.Show(currentGamer, parent, invoker, hideInvoker))
		{
			return false;
		}
		m_settingListBufferCount = 0;
		for (int i = 0; i < m_settingListElements.Length; i++)
		{
			SettingKind settingKind = (SettingKind)i;
			if (Array.IndexOf(modeUIData.m_supportedSettings, settingKind) != -1)
			{
				m_settingListElements[i].SetActive(true);
				m_settingListBuffer[m_settingListBufferCount++] = m_settingListElementSelectables[i];
				GameModeSettingElementUIController gameModeSettingElementUIController = m_settingListElements[i].RequestComponent<GameModeSettingElementUIController>();
				gameModeSettingElementUIController.SetData(settingKind, m_gameModeUIData.m_gameModeSettings[i], m_CachedEventSystem);
			}
		}
		for (int j = 0; j < m_settingListBufferCount; j++)
		{
			Navigation navigation = m_settingListBuffer[j].navigation;
			navigation.selectOnUp = m_settingListBuffer[(uint)(j - 1) % m_settingListBuffer.Length];
			navigation.selectOnDown = m_settingListBuffer[(uint)(j + 1) % m_settingListBuffer.Length];
			m_settingListBuffer[j].navigation = navigation;
		}
		if (m_settingListBufferCount > 0)
		{
			Navigation navigation2 = m_settingListBuffer[m_settingListBufferCount - 1].navigation;
			navigation2.selectOnDown = m_confirmButton;
			m_settingListBuffer[m_settingListBufferCount - 1].navigation = navigation2;
			Navigation navigation3 = m_confirmButton.navigation;
			navigation3.selectOnUp = m_settingListBuffer[m_settingListBufferCount - 1];
			m_confirmButton.navigation = navigation3;
			Navigation navigation4 = m_cancelButton.navigation;
			navigation4.selectOnUp = m_settingListBuffer[m_settingListBufferCount - 1];
			m_cancelButton.navigation = navigation4;
		}
		return true;
	}

	public override void Close()
	{
		GameSession gameSession = GameUtils.GetGameSession();
		if (gameSession.PendingGameModeSessionConfigChanges)
		{
			T17DialogBox dialog = T17DialogBoxManager.GetDialog(false);
			dialog.Initialize("Text.Warning", "Text.Menu.UnsavedChanges.Body", "Text.Button.Discard", "Text.Button.Save", null);
			dialog.OnConfirm = (T17DialogBox.DialogEvent)Delegate.Combine(dialog.OnConfirm, new T17DialogBox.DialogEvent(OnDialogConfirm));
			dialog.OnCancel = (T17DialogBox.DialogEvent)Delegate.Combine(dialog.OnCancel, new T17DialogBox.DialogEvent(OnDialogCancel));
			dialog.OnDecline = (T17DialogBox.DialogEvent)Delegate.Combine(dialog.OnDecline, new T17DialogBox.DialogEvent(OnDialogCancel));
			dialog.Show();
		}
		else
		{
			gameSession.RevertGameModeSessionConfig();
			base.Close();
		}
	}

	private void OnDialogConfirm()
	{
		GameSession gameSession = GameUtils.GetGameSession();
		gameSession.RevertGameModeSessionConfig();
		base.Close();
	}

	private void OnDialogCancel()
	{
		GameSession gameSession = GameUtils.GetGameSession();
		gameSession.CommitGameModeSessionConfig();
		base.Close();
	}

	public void Confirm()
	{
		GameSession gameSession = GameUtils.GetGameSession();
		gameSession.CommitGameModeSessionConfig();
		base.Close();
	}
}
