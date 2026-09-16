using System;
using InControl;
using Team17.Online;
using UnityEngine;

[ExecutionDependency(typeof(InControlManager))]
public class GamepadEngagementManager : Manager
{
	[SerializeField]
	[AssignResource("SidedAmbiControlsMappingData", Editorbility.NonEditable)]
	private AmbiControlsMappingData m_sidedAmbiMapping;

	[SerializeField]
	[AssignResource("UnsidedAmbiControlsMappingData", Editorbility.NonEditable)]
	private AmbiControlsMappingData m_unsidedAmbiMapping;

	private IPlayerManager m_iPlayerManager;

	private ILogicalButton[] m_cancelButtons = new ILogicalButton[4];

	private bool m_hasProfile;

	private readonly float kEngagementCooldownTime = 0.5f;

	private float m_engagementCooldownTimer;

	private bool m_canManuallyChangeEngagement;

	private SuppressionController m_manualEngagementSuppressor = new SuppressionController();

	public bool CanManuallyChangeEngagement
	{
		get
		{
			return m_canManuallyChangeEngagement;
		}
		set
		{
			m_canManuallyChangeEngagement = value;
		}
	}

	public SuppressionController Suppressor
	{
		get
		{
			return m_manualEngagementSuppressor;
		}
	}

	public void SetCanDisconnect(EngagementSlot _slot, bool _canDisconnect)
	{
	}

	private void Awake()
	{
		m_iPlayerManager = GameUtils.RequireManagerInterface<IPlayerManager>();
		m_iPlayerManager.EngagementChangeCallback += OnEngagementChanged;
		m_hasProfile = m_iPlayerManager.HasPlayer();
		PlayerInputLookup.OnRegenerateControls = (CallbackVoid)Delegate.Combine(PlayerInputLookup.OnRegenerateControls, new CallbackVoid(GatherButtons));
		if (PlayerInputLookup.IsAwake())
		{
			GatherButtons();
		}
	}

	private void GatherButtons()
	{
		for (int i = 0; i < 4; i++)
		{
			PlayerGameInput playerGameInput = new PlayerGameInput((ControlPadInput.PadNum)i, PadSide.Both, m_unsidedAmbiMapping);
			m_cancelButtons[i] = PlayerInputLookup.GetFixedButton(PlayerInputLookup.LogicalButtonID.UICancel, playerGameInput);
		}
	}

	private void OnDestroy()
	{
		m_iPlayerManager.EngagementChangeCallback -= OnEngagementChanged;
	}

	private void OnEngagementChanged(EngagementSlot _e, GamepadUser _prev, GamepadUser _new)
	{
		if (_e == EngagementSlot.One)
		{
			if (_prev != null && _new == null)
			{
				m_hasProfile = false;
			}
			if (_new != null && _prev == null)
			{
				m_hasProfile = true;
			}
		}
		PlayerGameInput playerGameInput = new PlayerGameInput((ControlPadInput.PadNum)_e, PadSide.Both, m_unsidedAmbiMapping);
		m_cancelButtons[(int)_e] = PlayerInputLookup.GetFixedButton(PlayerInputLookup.LogicalButtonID.UICancel, playerGameInput);
	}

	private void Update()
	{
		if (m_hasProfile && !m_iPlayerManager.IsBusy() && !T17DialogBoxManager.HasAnyOpenDialogs() && m_engagementCooldownTimer <= 0f && ClientGameSetup.Mode == GameMode.OnlineKitchen && m_canManuallyChangeEngagement && !m_manualEngagementSuppressor.IsSuppressed())
		{
			if (ClientUserSystem.m_Users.Count < 4)
			{
				EngagmentCircumstances o_circumstances = null;
				ControlPadInput.PadNum engagementPad = m_iPlayerManager.GetEngagementPad(out o_circumstances);
				if (engagementPad != ControlPadInput.PadNum.Count && engagementPad != ControlPadInput.PadNum.One && m_iPlayerManager.HasFreeEngagementSlot())
				{
					m_iPlayerManager.StartPadEngagement(engagementPad, o_circumstances, null);
					m_engagementCooldownTimer = kEngagementCooldownTime;
				}
			}
			for (int i = 0; i < 4; i++)
			{
				if (m_cancelButtons[i].JustPressed())
				{
					GamepadUser user = m_iPlayerManager.GetUser((EngagementSlot)i);
					if (user != null && !user.StickyEngagement)
					{
						m_iPlayerManager.DisengagePad((EngagementSlot)i);
						m_engagementCooldownTimer = kEngagementCooldownTime;
					}
				}
			}
		}
		if (m_engagementCooldownTimer > 0f)
		{
			m_engagementCooldownTimer -= Time.deltaTime;
		}
		if (m_manualEngagementSuppressor != null)
		{
			m_manualEngagementSuppressor.UpdateSuppressors();
		}
	}
}
