using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerInputReceiver : ServerSynchroniserBase
{
	private OrderedMessageReceivedCallback m_NetworkInputMessageReceived;

	private uint m_ChefEntityID;

	private NetworkLogicalButton m_DashNetworkButton;

	private NetworkLogicalButton m_CurseNetworkButton;

	private NetworkLogicalButton m_PickupNetworkButton;

	private NetworkLogicalButton m_WorkstationNetworkButton;

	private NetworkLogicalValue m_NetworkAxisX;

	private NetworkLogicalValue m_NetworkAxisY;

	private Transform m_Transform;

	private PlayerIDProvider m_PlayerIDProvider;

	private PlayerControls m_PlayerControls;

	private float m_LastReceivedTimeStamp;

	private float m_LastReceivedTimeStampServerTime;

	private OrderedMessageReceivedCallback m_gameStateChanged;

	private float m_lastUpdatedTime;

	public void Awake()
	{
		m_NetworkInputMessageReceived = InputServerMessageReceived;
		Mailbox.Server.RegisterForMessageType(MessageType.Input, m_NetworkInputMessageReceived);
		m_Transform = base.transform;
		m_PlayerIDProvider = GetComponent<PlayerIDProvider>();
		m_PlayerControls = GetComponent<PlayerControls>();
		m_gameStateChanged = OnGameStateChanged;
		Mailbox.Server.RegisterForMessageType(MessageType.GameState, m_gameStateChanged);
	}

	private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		GameStateMessage gameStateMessage = (GameStateMessage)message;
		if (gameStateMessage.m_State == GameState.AssignedChefsToUsers)
		{
			Setup();
		}
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(base.gameObject);
		if (entry != null)
		{
			m_ChefEntityID = entry.m_Header.m_uEntityID;
		}
	}

	public override void OnDestroy()
	{
		Mailbox.Server.UnregisterForMessageType(MessageType.Input, m_NetworkInputMessageReceived);
		Mailbox.Server.UnregisterForMessageType(MessageType.GameState, m_gameStateChanged);
	}

	private void InputServerMessageReceived(IOnlineMultiplayerSessionUserId _userID, Serialisable _serialisable)
	{
		ControllerStateMessage controllerStateMessage = _serialisable as ControllerStateMessage;
		bool flag = false;
		if (controllerStateMessage == null || controllerStateMessage.m_uChefEntityID != m_ChefEntityID)
		{
			return;
		}
		if (m_DashNetworkButton != null)
		{
			m_DashNetworkButton.SetIsDown(controllerStateMessage.IsButtonDown(PlayerInputLookup.LogicalButtonID.Dash));
			flag = true;
		}
		if (m_CurseNetworkButton != null)
		{
			m_CurseNetworkButton.SetIsDown(controllerStateMessage.IsButtonDown(PlayerInputLookup.LogicalButtonID.Curse));
		}
		if (m_PickupNetworkButton != null)
		{
			m_PickupNetworkButton.SetIsDown(controllerStateMessage.IsButtonDown(PlayerInputLookup.LogicalButtonID.PickupAndDrop));
		}
		if (m_WorkstationNetworkButton != null)
		{
			m_WorkstationNetworkButton.SetIsDown(controllerStateMessage.IsButtonDown(PlayerInputLookup.LogicalButtonID.WorkstationInteract));
		}
		if (m_NetworkAxisX != null)
		{
			m_NetworkAxisX.SetValue(controllerStateMessage.m_AxisX);
			flag = true;
		}
		if (m_NetworkAxisY != null)
		{
			m_NetworkAxisY.SetValue(controllerStateMessage.m_AxisY);
			flag = true;
		}
		if (null != m_Transform && m_PlayerControls != null && m_PlayerControls.enabled)
		{
			m_Transform.rotation = controllerStateMessage.rotation;
		}
		if (flag && !(controllerStateMessage.m_Time < m_LastReceivedTimeStamp))
		{
			if (controllerStateMessage.m_Time < GetLastClientInputTimeStamp())
			{
				if (Time.time > m_lastUpdatedTime + 0.005f)
				{
					m_LastReceivedTimeStamp = controllerStateMessage.m_Time;
					m_LastReceivedTimeStampServerTime = Time.time;
				}
				else
				{
					Debug.Log("SHOULD BE IMPOSSIBLE");
				}
			}
			else if (controllerStateMessage.m_Time > GetLastClientInputTimeStamp() + 0.45f)
			{
				m_LastReceivedTimeStamp = controllerStateMessage.m_Time;
				m_LastReceivedTimeStampServerTime = Time.time;
			}
			else if (controllerStateMessage.m_Time > GetLastClientInputTimeStamp() + 0.015f)
			{
				Debug.Log("AheadMessageRecived");
				if (Time.time > m_lastUpdatedTime + 0.005f)
				{
					m_LastReceivedTimeStampServerTime -= 0.015f;
				}
				else
				{
					Debug.Log("MultiMessageRecived");
				}
			}
			else
			{
				m_LastReceivedTimeStamp = controllerStateMessage.m_Time;
				m_LastReceivedTimeStampServerTime = Time.time;
			}
			m_lastUpdatedTime = Time.time;
		}
		if (m_PlayerControls != null && !m_PlayerIDProvider.IsLocallyControlled())
		{
			m_PlayerControls.SetDirectlyUnderPlayerControl(controllerStateMessage.m_underControl);
		}
	}

	private void Setup()
	{
		bool flag = m_PlayerIDProvider.IsLocallyControlled();
		PlayerControls component = GetComponent<PlayerControls>();
		if (component != null && !flag)
		{
			PlayerInputLookup.Player iD = m_PlayerIDProvider.GetID();
			PlayerControls.ControlSchemeData controlSchemeData = new PlayerControls.ControlSchemeData(iD, component);
			bool buttonRequiresAppFocus = m_PlayerIDProvider.IsLocallyControlled();
			m_DashNetworkButton = (NetworkLogicalButton)(controlSchemeData.m_dashButton = new NetworkLogicalButton(controlSchemeData.m_dashButton, PlayerInputLookup.LogicalButtonID.Dash, buttonRequiresAppFocus));
			m_CurseNetworkButton = (NetworkLogicalButton)(controlSchemeData.m_curseButton = new NetworkLogicalButton(controlSchemeData.m_curseButton, PlayerInputLookup.LogicalButtonID.Curse, buttonRequiresAppFocus));
			m_PickupNetworkButton = (NetworkLogicalButton)(controlSchemeData.m_pickupButton = new NetworkLogicalButton(controlSchemeData.m_pickupButton, PlayerInputLookup.LogicalButtonID.PickupAndDrop, buttonRequiresAppFocus));
			m_WorkstationNetworkButton = (NetworkLogicalButton)(controlSchemeData.m_worksurfaceUseButton = new NetworkLogicalButton(controlSchemeData.m_worksurfaceUseButton, PlayerInputLookup.LogicalButtonID.WorkstationInteract, buttonRequiresAppFocus));
			m_NetworkAxisX = (NetworkLogicalValue)(controlSchemeData.m_moveX = new NetworkLogicalValue(controlSchemeData.m_moveX, PlayerInputLookup.LogicalValueID.MovementX));
			m_NetworkAxisY = (NetworkLogicalValue)(controlSchemeData.m_moveY = new NetworkLogicalValue(controlSchemeData.m_moveY, PlayerInputLookup.LogicalValueID.MovementY));
			component.SetControlSchemeData(controlSchemeData);
			component.SetServerControlled(true);
		}
	}

	public float GetLastClientInputTimeStamp()
	{
		return m_LastReceivedTimeStamp + (Time.time - m_LastReceivedTimeStampServerTime);
	}
}
