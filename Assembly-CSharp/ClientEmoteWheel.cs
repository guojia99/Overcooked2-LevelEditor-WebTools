using System;
using System.Collections;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientEmoteWheel : ClientSynchroniserBase
{
	private EmoteWheel m_emoteWheel;

	private EmoteWheelMessage m_message = new EmoteWheelMessage();

	private IEnumerator m_emoteRoutine;

	private GameObject m_emoteDialog;

	private Suppressor m_suppressor;

	private IPlayerManager m_iPlayerManager;

	private GamepadUser m_gamepadOnLastInputRefresh;

	private PlayerInputLookup.Player m_playerIDOnLastInputRefresh = PlayerInputLookup.Player.Count;

	private ILogicalButton m_wheelButton;

	private ILogicalButton m_rightButton;

	private ILogicalButton m_downButton;

	private ILogicalButton m_leftButton;

	private ILogicalButton m_upButton;

	private ILogicalButton m_renableInputButton;

	private ILogicalValue m_xMovement;

	private ILogicalValue m_yMovement;

	private EmoteSelector m_emoteSelector;

	private Coroutine m_enableInputRoutine;

	protected virtual void Awake()
	{
		m_emoteWheel = base.gameObject.RequireComponent<EmoteWheel>();
		Mailbox.Client.RegisterForMessageType(MessageType.EmoteWheel, ProcessServerMessage);
		m_iPlayerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
		RefreshInputVars();
		PlayerInputLookup.OnRegenerateControls = (CallbackVoid)Delegate.Combine(PlayerInputLookup.OnRegenerateControls, new CallbackVoid(OnRegenerateControls));
	}

	protected virtual void Start()
	{
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Combine(ClientUserSystem.usersChanged, new GenericVoid(OnUsersChanged));
		m_iPlayerManager.EngagementChangeCallback += OnEngagementChanged;
		m_emoteSelector = new EmoteSelector(m_emoteWheel, base.transform);
		m_emoteSelector.Hide();
		if (m_emoteWheel.m_animationTarget == null)
		{
			m_emoteWheel.m_animationTarget = base.gameObject.RequestComponentInImmediateChildren<Animator>();
		}
	}

	private PlayerInputLookup.Player NetworkToLocalPlayerNumber(PlayerInputLookup.Player _player)
	{
		int num = 0;
		for (int i = 0; i < ClientUserSystem.m_Users.Count; i++)
		{
			if (i == (int)_player)
			{
				if (!ClientUserSystem.m_Users._items[i].IsLocal)
				{
					break;
				}
				return (PlayerInputLookup.Player)num;
			}
			if (ClientUserSystem.m_Users._items[i].IsLocal)
			{
				num++;
			}
		}
		return PlayerInputLookup.Player.Count;
	}

	protected void ProcessServerMessage(IOnlineMultiplayerSessionUserId _sessionId, Serialisable _serialisable)
	{
		EmoteWheelMessage emoteWheelMessage = _serialisable as EmoteWheelMessage;
		if (emoteWheelMessage.m_player == m_emoteWheel.m_player && emoteWheelMessage.m_player != PlayerInputLookup.Player.Count && !m_emoteWheel.IsLocal && emoteWheelMessage.m_forUI == m_emoteWheel.ForUI)
		{
			StartEmote(emoteWheelMessage.m_emoteIdx);
		}
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
		if (!m_emoteWheel.ForUI)
		{
			UpdateEmoteWheel();
		}
	}

	private void Update()
	{
		if (m_emoteWheel.ForUI)
		{
			UpdateEmoteWheel();
		}
	}

	private void UpdateEmoteWheel()
	{
		if (m_emoteRoutine != null)
		{
			m_emoteRoutine.MoveNext();
		}
		bool flag = m_emoteWheel.CanShow();
		if (!flag && m_emoteSelector.IsActive())
		{
			m_emoteSelector.Hide();
			if (m_enableInputRoutine != null)
			{
				StopCoroutine(m_enableInputRoutine);
				m_enableInputRoutine = null;
			}
			ForceReEnableInput();
		}
		if (!flag || m_wheelButton == null || (m_emoteWheel.ForUI && !ConnectionStatus.IsInSession() && UserSystemUtils.AnySplitPadUsers()))
		{
			return;
		}
		if (!m_emoteSelector.IsActive())
		{
			if (m_wheelButton.JustPressed())
			{
				OpenWheel();
			}
		}
		else if (m_wheelButton.IsDown())
		{
			PlayerInputLookup.Player player = ((!(m_emoteWheel.m_playerIDProvider != null)) ? NetworkToLocalPlayerNumber(m_emoteWheel.m_player) : m_emoteWheel.m_playerIDProvider.GetID());
			if (PCPadInputProvider.IsKeyboard(PlayerInputLookup.GetPadForPlayer(player)))
			{
				ProcessDirectionalInput();
			}
			else
			{
				float x = m_xMovement.GetValue() * m_emoteWheel.m_emoteWheelOptions.m_radius;
				float y = (0f - m_yMovement.GetValue()) * m_emoteWheel.m_emoteWheelOptions.m_radius;
				m_emoteSelector.AnalogUpdate(x, y);
			}
			if (Input.mousePresent)
			{
				if (Input.GetMouseButtonDown(0))
				{
					CloseWheel();
				}
				else
				{
					m_emoteSelector.PointerUpdate();
				}
			}
		}
		else
		{
			CloseWheel();
		}
	}

	private IEnumerator EmoteRoutine(int _emoteIdx)
	{
		EmoteWheelOption emote = m_emoteWheel.m_emoteWheelOptions.m_options[_emoteIdx];
		if ((emote.m_type == EmoteWheelOption.EmoteType.Animation || emote.m_type == EmoteWheelOption.EmoteType.Both) && !emote.m_triggerForCode && m_emoteWheel.m_animationTarget != null)
		{
			m_emoteWheel.m_animationTarget.SetTrigger(emote.m_animTriggerHash);
		}
		if (emote.m_type != EmoteWheelOption.EmoteType.Dialog && emote.m_type != EmoteWheelOption.EmoteType.Both)
		{
			yield break;
		}
		if (m_emoteWheel.ForUI)
		{
			if (emote.m_dialogPrefab != null)
			{
				m_emoteDialog = UnityEngine.Object.Instantiate(emote.m_dialogPrefab);
				m_emoteDialog.transform.SetParent(m_emoteWheel.m_uiPlayer.DialogAnchor, false);
				RectTransform rectTransform = m_emoteDialog.transform as RectTransform;
				rectTransform.anchoredPosition = new Vector2(emote.m_anchorOffset.x, emote.m_anchorOffset.y);
			}
		}
		else if (emote.m_dialogPrefab != null)
		{
			m_emoteDialog = GameUtils.InstantiateUIController(emote.m_dialogPrefab, "HoverIconCanvas");
			HoverIconUIController hoverIconUIController = m_emoteDialog.RequestComponent<HoverIconUIController>();
			if (hoverIconUIController == null)
			{
				hoverIconUIController = m_emoteDialog.AddComponent<HoverIconUIController>();
			}
			if (hoverIconUIController != null)
			{
				hoverIconUIController.SetFollowTransform(base.transform, emote.m_anchorOffset);
				hoverIconUIController.ShouldFollow = true;
			}
		}
		IEnumerator delay = CoroutineUtils.TimerRoutine(m_emoteWheel.m_emoteWheelOptions.m_options[_emoteIdx].m_duration, base.gameObject.layer);
		while (delay.MoveNext() && (!(m_emoteWheel.m_playerRespawn != null) || !m_emoteWheel.m_playerRespawn.IsRespawning))
		{
			yield return null;
		}
		CleanUpEmote();
	}

	private void OpenWheel()
	{
		if (m_enableInputRoutine != null)
		{
			StopCoroutine(m_enableInputRoutine);
			m_enableInputRoutine = null;
		}
		if (m_emoteWheel.ForUI)
		{
			DisableEventSystem();
		}
		else
		{
			IPlayerControlsImpl activeControlsImpl = m_emoteWheel.m_playerControls.GetActiveControlsImpl();
			if (activeControlsImpl == null)
			{
				return;
			}
			if (m_suppressor == null)
			{
				m_suppressor = m_emoteWheel.m_playerControls.Suppress(this);
			}
		}
		m_emoteSelector.Show();
	}

	public void StartEmote(int _emoteIdx)
	{
		if (m_emoteRoutine != null)
		{
			CleanUpEmote();
		}
		m_emoteRoutine = EmoteRoutine(_emoteIdx);
	}

	public void RequestEmoteStart(int _emoteIdx)
	{
		m_message.InitialiseStartEmote(_emoteIdx, m_emoteWheel.m_player, m_emoteWheel.ForUI);
		ClientMessenger.EmoteWheelMessage(m_message);
		StartEmote(_emoteIdx);
	}

	private void CloseWheel()
	{
		int selected = m_emoteSelector.GetSelected();
		if (selected != -1)
		{
			RequestEmoteStart(selected);
			string emoteId = m_emoteWheel.m_emoteWheelOptions.m_options[selected].m_emoteId;
			if (emoteId != null)
			{
				OvercookedAchievementManager overcookedAchievementManager = GameUtils.RequestManager<OvercookedAchievementManager>();
				if (overcookedAchievementManager != null)
				{
					PlayerInputLookup.Player player = ((!m_emoteWheel.ForUI) ? m_emoteWheel.m_playerIDProvider.GetID() : m_emoteWheel.m_player);
					overcookedAchievementManager.AddIDStat(21, emoteId.GetHashCode(), PlayerInputLookup.GetPadForPlayer(player));
				}
			}
		}
		m_emoteSelector.Hide();
		if (m_enableInputRoutine == null)
		{
			m_enableInputRoutine = StartCoroutine(ReEnableInputRoutine(new Vector2(m_xMovement.GetValue(), m_yMovement.GetValue())));
		}
	}

	private IEnumerator ReEnableInputRoutine(Vector2 _posOnClose)
	{
		while (m_xMovement != null && m_yMovement != null && (m_xMovement.GetValue() != 0f || m_yMovement.GetValue() != 0f) && (m_emoteWheel.ForUI || !m_renableInputButton.IsDown()))
		{
			float num = m_emoteWheel.m_analogEnableThreshold * m_emoteWheel.m_analogEnableThreshold;
			Vector2 vector = new Vector2(m_xMovement.GetValue(), m_yMovement.GetValue());
			if (Mathf.Abs((_posOnClose - vector).sqrMagnitude) > num)
			{
				break;
			}
			yield return null;
		}
		ForceReEnableInput();
	}

	private void ForceReEnableInput()
	{
		if (m_suppressor != null)
		{
			if (m_emoteWheel.ForUI)
			{
				m_suppressor.Release();
			}
			else
			{
				m_emoteWheel.m_playerControls.ReleaseSuppressor(m_suppressor);
			}
			m_suppressor = null;
		}
		m_enableInputRoutine = null;
	}

	private void ProcessDirectionalInput()
	{
		EmoteWheelOption.Connection.Direction direction = EmoteWheelOption.Connection.Direction.COUNT;
		bool flag = m_rightButton.JustPressed();
		bool flag2 = m_downButton.JustPressed();
		bool flag3 = m_leftButton.JustPressed();
		if (m_upButton.JustPressed())
		{
			direction = (flag3 ? EmoteWheelOption.Connection.Direction.UpLeft : ((!flag) ? EmoteWheelOption.Connection.Direction.Up : EmoteWheelOption.Connection.Direction.UpRight));
		}
		else if (flag2)
		{
			direction = (flag3 ? EmoteWheelOption.Connection.Direction.DownLeft : (flag ? EmoteWheelOption.Connection.Direction.DownRight : EmoteWheelOption.Connection.Direction.Down));
		}
		else if (flag3)
		{
			direction = EmoteWheelOption.Connection.Direction.Left;
		}
		else if (flag)
		{
			direction = EmoteWheelOption.Connection.Direction.Right;
		}
		if (direction != EmoteWheelOption.Connection.Direction.COUNT)
		{
			m_emoteSelector.Update(direction);
		}
	}

	private void DisableEventSystem()
	{
		if (m_suppressor != null)
		{
			return;
		}
		PlayerInputLookup.Player player = NetworkToLocalPlayerNumber(m_emoteWheel.m_player);
		if (player == PlayerInputLookup.Player.Count)
		{
			return;
		}
		GamepadUser user = m_iPlayerManager.GetUser((EngagementSlot)player);
		if (user != null)
		{
			T17EventSystem eventSystemForGamepadUser = T17EventSystemsManager.Instance.GetEventSystemForGamepadUser(user);
			if (eventSystemForGamepadUser != null)
			{
				m_suppressor = eventSystemForGamepadUser.Disable(this);
			}
		}
	}

	protected void CleanUpEmote()
	{
		if (m_emoteDialog != null)
		{
			UnityEngine.Object.Destroy(m_emoteDialog);
		}
		m_emoteRoutine = null;
	}

	protected void OnUsersChanged()
	{
		RefreshInputVars();
		if (m_emoteWheel.m_animationTarget == null)
		{
			m_emoteWheel.m_animationTarget = base.gameObject.RequestComponentInImmediateChildren<Animator>();
		}
	}

	private void OnRegenerateControls()
	{
		RefreshInputVars(true);
	}

	private void RefreshInputVars(bool _force = false)
	{
		PlayerInputLookup.Player player = ((!m_emoteWheel.ForUI) ? m_emoteWheel.m_playerIDProvider.GetID() : NetworkToLocalPlayerNumber(m_emoteWheel.m_player));
		User user = null;
		int num = 0;
		for (int i = 0; i < ClientUserSystem.m_Users.Count; i++)
		{
			User user2 = ClientUserSystem.m_Users._items[i];
			if (user2.IsLocal)
			{
				if (num == (int)player)
				{
					user = user2;
					break;
				}
				num++;
			}
		}
		GamepadUser gamepadUser = ((user == null) ? null : user.GamepadUser);
		if (_force || (gamepadUser != null && (m_gamepadOnLastInputRefresh != gamepadUser || m_playerIDOnLastInputRefresh != player)))
		{
			if (player != PlayerInputLookup.Player.Count)
			{
				m_wheelButton = PlayerInputLookup.GetButton(PlayerInputLookup.LogicalButtonID.Curse, player);
				m_xMovement = PlayerInputLookup.GetValue(PlayerInputLookup.LogicalValueID.MovementX, player);
				m_yMovement = PlayerInputLookup.GetValue(PlayerInputLookup.LogicalValueID.MovementY, player);
				m_rightButton = PlayerInputLookup.GetButton(PlayerInputLookup.LogicalButtonID.UIRight, player);
				m_downButton = PlayerInputLookup.GetButton(PlayerInputLookup.LogicalButtonID.UIDown, player);
				m_leftButton = PlayerInputLookup.GetButton(PlayerInputLookup.LogicalButtonID.UILeft, player);
				m_upButton = PlayerInputLookup.GetButton(PlayerInputLookup.LogicalButtonID.UIUp, player);
				m_renableInputButton = new ComboLogicalButton(new ILogicalButton[3]
				{
					PlayerInputLookup.GetButton(PlayerInputLookup.LogicalButtonID.Dash, player),
					PlayerInputLookup.GetButton(PlayerInputLookup.LogicalButtonID.PickupAndDrop, player),
					PlayerInputLookup.GetButton(PlayerInputLookup.LogicalButtonID.WorkstationInteract, player)
				});
			}
			else
			{
				m_wheelButton = null;
				m_xMovement = null;
				m_yMovement = null;
				m_rightButton = null;
				m_downButton = null;
				m_leftButton = null;
				m_upButton = null;
				m_renableInputButton = null;
			}
			m_gamepadOnLastInputRefresh = gamepadUser;
			m_playerIDOnLastInputRefresh = player;
		}
	}

	private void OnEngagementChanged(EngagementSlot _slot, GamepadUser _old, GamepadUser _new)
	{
		RefreshInputVars();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		Mailbox.Client.UnregisterForMessageType(MessageType.EmoteWheel, ProcessServerMessage);
		ClientUserSystem.usersChanged = (GenericVoid)Delegate.Remove(ClientUserSystem.usersChanged, new GenericVoid(OnUsersChanged));
		m_iPlayerManager.EngagementChangeCallback -= OnEngagementChanged;
		if (m_suppressor != null)
		{
			m_suppressor.Release();
			m_suppressor = null;
		}
		if (m_emoteSelector != null)
		{
			m_emoteSelector.Destroy();
		}
		PlayerInputLookup.OnRegenerateControls = (CallbackVoid)Delegate.Remove(PlayerInputLookup.OnRegenerateControls, new CallbackVoid(OnRegenerateControls));
	}
}
