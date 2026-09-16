using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientInputTransmitter : ClientSynchroniserBase
{
	private class Pad
	{
		public ILogicalValue m_Y;

		public ILogicalValue m_X;

		public ILogicalButton m_Dash;

		public ILogicalButton m_Curse;

		public ILogicalButton m_Worksurface;

		public ILogicalButton m_Pickup;
	}

	private Pad m_Pad;

	private bool m_bForceSend;

	private bool m_bInControl;

	private EntitySerialisationEntry m_SerialisationEntry;

	private ControllerStateMessage m_PrevControllerState = new ControllerStateMessage();

	private ControllerStateMessage m_ControllerState = new ControllerStateMessage();

	private Generic<bool> m_CanButtonBePressed;

	private Transform m_Transform;

	private PlayerControls m_playerControls;

	private bool m_bIsServer;

	public void Awake()
	{
		m_CanButtonBePressed = GetComponent<PlayerControls>().CanButtonBePressed;
		m_Transform = base.transform;
	}

	protected override void OnDestroy()
	{
		m_SerialisationEntry = null;
		m_CanButtonBePressed = null;
		base.OnDestroy();
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_SerialisationEntry = EntitySerialisationRegistry.GetEntry(base.gameObject);
		if (m_SerialisationEntry != null)
		{
			m_ControllerState.m_uChefEntityID = m_SerialisationEntry.m_Header.m_uEntityID;
			m_PrevControllerState.m_uChefEntityID = m_SerialisationEntry.m_Header.m_uEntityID;
		}
		m_bForceSend = true;
		m_bIsServer = GetComponent<ServerInputReceiver>() != null;
	}

	public override void UpdateSynchronising()
	{
		if (!m_bIsServer && IsInControl() && m_Pad != null)
		{
			float value = m_Pad.m_X.GetValue();
			float value2 = m_Pad.m_Y.GetValue();
			bool flag = m_Pad.m_Dash.IsDown();
			bool flag2 = m_Pad.m_Curse.IsDown();
			bool flag3 = m_Pad.m_Pickup.IsDown();
			bool flag4 = m_Pad.m_Worksurface.IsDown();
			m_ControllerState.m_AxisX = value;
			m_ControllerState.m_AxisY = value2;
			if (flag != m_PrevControllerState.IsButtonDown(PlayerInputLookup.LogicalButtonID.Dash))
			{
				m_ControllerState.SetButtonPressed(PlayerInputLookup.LogicalButtonID.Dash, flag);
			}
			if (flag2 != m_PrevControllerState.IsButtonDown(PlayerInputLookup.LogicalButtonID.Curse))
			{
				m_ControllerState.SetButtonPressed(PlayerInputLookup.LogicalButtonID.Curse, flag2);
			}
			if (flag3 != m_PrevControllerState.IsButtonDown(PlayerInputLookup.LogicalButtonID.PickupAndDrop))
			{
				m_ControllerState.SetButtonPressed(PlayerInputLookup.LogicalButtonID.PickupAndDrop, flag3);
			}
			if (flag4 != m_PrevControllerState.IsButtonDown(PlayerInputLookup.LogicalButtonID.WorkstationInteract))
			{
				m_ControllerState.SetButtonPressed(PlayerInputLookup.LogicalButtonID.WorkstationInteract, flag4);
			}
			m_ControllerState.m_underControl = m_playerControls.GetDirectlyUnderPlayerControl();
			if (m_PrevControllerState.IsDifferent(m_ControllerState) || m_bForceSend)
			{
				m_ControllerState.rotation = m_Transform.rotation;
				m_ControllerState.m_Time = Time.time;
				ClientMessenger.ControllerState(m_ControllerState);
				m_PrevControllerState.Copy(m_ControllerState);
				m_bForceSend = false;
			}
		}
	}

	public bool IsInControl()
	{
		return m_bInControl;
	}

	public void Setup()
	{
		PlayerIDProvider component = GetComponent<PlayerIDProvider>();
		PlayerInputLookup.Player iD = component.GetID();
		m_bInControl = iD != PlayerInputLookup.Player.Count;
		if (m_bInControl)
		{
			m_Pad = new Pad();
			m_Pad.m_Y = GetGated(PlayerInputLookup.GetValue(PlayerInputLookup.LogicalValueID.MovementY, iD));
			m_Pad.m_X = GetGated(PlayerInputLookup.GetValue(PlayerInputLookup.LogicalValueID.MovementX, iD));
			m_Pad.m_Dash = GetGated(PlayerInputLookup.GetButton(PlayerInputLookup.LogicalButtonID.Dash, iD));
			m_Pad.m_Curse = GetGated(PlayerInputLookup.GetButton(PlayerInputLookup.LogicalButtonID.Curse, iD));
			m_Pad.m_Worksurface = GetGated(PlayerInputLookup.GetButton(PlayerInputLookup.LogicalButtonID.WorkstationInteract, iD));
			m_Pad.m_Pickup = GetGated(PlayerInputLookup.GetButton(PlayerInputLookup.LogicalButtonID.PickupAndDrop, iD));
			m_playerControls = GetComponent<PlayerControls>();
			if (m_playerControls != null)
			{
				PlayerControls.ControlSchemeData controlSchemeData = new PlayerControls.ControlSchemeData(iD, m_playerControls);
				controlSchemeData.m_dashButton = m_Pad.m_Dash;
				controlSchemeData.m_curseButton = m_Pad.m_Curse;
				controlSchemeData.m_pickupButton = m_Pad.m_Pickup;
				controlSchemeData.m_worksurfaceUseButton = m_Pad.m_Worksurface;
				controlSchemeData.m_moveX = m_Pad.m_X;
				controlSchemeData.m_moveY = m_Pad.m_Y;
				m_playerControls.SetControlSchemeData(controlSchemeData);
			}
		}
	}

	private ILogicalButton GetGated(ILogicalButton _toProtect)
	{
		return new GateLogicalButton(_toProtect, m_CanButtonBePressed);
	}

	private ILogicalValue GetGated(ILogicalValue _toProtect)
	{
		return new GateLogicalValue(_toProtect, m_CanButtonBePressed);
	}
}
