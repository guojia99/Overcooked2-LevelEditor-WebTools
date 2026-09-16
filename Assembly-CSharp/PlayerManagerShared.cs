using System.Collections;
using System.Collections.Generic;
using Team17.Online;
using UnityEngine;
using UnityEngine.UI;

public abstract class PlayerManagerShared<GamepadUserType> : Manager, IPlayerManager where GamepadUserType : GamepadUser
{
	public class EngagementSlotComparer : IEqualityComparer<EngagementSlot>
	{
		public bool Equals(EngagementSlot x, EngagementSlot y)
		{
			return x == y;
		}

		public int GetHashCode(EngagementSlot obj)
		{
			return (int)obj;
		}
	}

	public class PadComparer : IEqualityComparer<ControlPadInput.PadNum>
	{
		public bool Equals(ControlPadInput.PadNum x, ControlPadInput.PadNum y)
		{
			return x == y;
		}

		public int GetHashCode(ControlPadInput.PadNum obj)
		{
			return (int)obj;
		}
	}

	private class EngagementRoutine
	{
		public EngagementSlot Slot;

		public IEnumerator Routine;

		public EngagementRoutine(EngagementSlot _slot, IEnumerator _routine)
		{
			Slot = _slot;
			Routine = _routine;
		}
	}

	[SerializeField]
	[AssignChildRecursive("Dimmer", Editorbility.NonEditable)]
	private GameObject m_dimmer;

	[SerializeField]
	[AssignChildRecursive("RejoinUI", Editorbility.NonEditable)]
	private GameObject m_rejoin;

	[SerializeField]
	private Text m_rejoinGamerNameText;

	[SerializeField]
	[AssignResource("SidedAmbiControlsMappingData", Editorbility.NonEditable)]
	private AmbiControlsMappingData m_sidedAmbiMapping;

	[SerializeField]
	[AssignResource("UnsidedAmbiControlsMappingData", Editorbility.NonEditable)]
	private AmbiControlsMappingData m_unsidedAmbiMapping;

	protected Dictionary<EngagementSlot, GamepadUserType> m_engagedPads = new Dictionary<EngagementSlot, GamepadUserType>(new EngagementSlotComparer());

	protected Dictionary<ControlPadInput.PadNum, ILogicalButton> m_engagementButtons = new Dictionary<ControlPadInput.PadNum, ILogicalButton>(new PadComparer());

	private List<EngagementRoutine> m_engagementRoutines = new List<EngagementRoutine>();

	private TimeManager.PauseLayer? m_lostEngagedPauseLayer;

	private Suppressor m_lostEngagedSuppressor;

	protected List<GamepadUserType> m_lostUsers = new List<GamepadUserType>();

	private GamepadUserType m_cachedPrimaryUser;

	protected const int c_numEngagementSlots = 4;

	protected static bool c_AcceptInverted;

	public bool CanChangeSplitPads;

	protected GameObject Dimmer
	{
		get
		{
			return m_dimmer;
		}
	}

	protected GameObject Rejoin
	{
		get
		{
			return m_rejoin;
		}
	}

	public AmbiControlsMappingData SidedAmbiMapping
	{
		get
		{
			return m_sidedAmbiMapping;
		}
	}

	public AmbiControlsMappingData UnsidedAmbiMapping
	{
		get
		{
			return m_unsidedAmbiMapping;
		}
	}

	public static bool AcceptAndCancelButtonsInverted
	{
		get
		{
			return c_AcceptInverted;
		}
	}

	private event VoidGeneric<EngagementSlot, GamepadUser, GamepadUser> m_engagementCallback = delegate
	{
	};

	public event VoidGeneric<EngagementSlot, GamepadUser, GamepadUser> EngagementChangeCallback
	{
		add
		{
			m_engagementCallback += value;
		}
		remove
		{
			m_engagementCallback -= value;
		}
	}

	private static void _AOT()
	{
		new EngagementSlot[0].Stringify();
		ArrayUtils.AssembleString(EngagementSlot.One, string.Empty);
		new EngagementSlot[0].Collapse<EngagementSlot, string>(ArrayUtils.AssembleString);
	}

	public virtual GamepadUser GetUser(EngagementSlot _slot)
	{
		GamepadUserType value = (GamepadUserType)null;
		m_engagedPads.TryGetValue(_slot, out value);
		return value;
	}

	public GamepadUser GetCachedPrimaryUser()
	{
		return m_cachedPrimaryUser;
	}

	protected void ClearEngagement()
	{
		Dictionary<EngagementSlot, GamepadUserType> engagedPads = m_engagedPads;
		m_engagedPads = new Dictionary<EngagementSlot, GamepadUserType>();
		m_lostUsers.Clear();
		ServerUserSystem.UnlockEngagement();
		foreach (KeyValuePair<EngagementSlot, GamepadUserType> item in engagedPads)
		{
			CallEngagementChangeCallback(item.Key, item.Value, null);
		}
		FastList<User> users = ServerUserSystem.m_Users;
		User.MachineID s_LocalMachineId = ServerUserSystem.s_LocalMachineId;
		User user = UserSystemUtils.FindUser(users, null, s_LocalMachineId, EngagementSlot.One);
		if (user != null)
		{
			ServerUserSystem.RemoveUser(user);
		}
		m_engagementRoutines.Clear();
		FinishLostPadEngagement();
	}

	public virtual ControlPadInput.PadNum GetEngagementPad(out EngagmentCircumstances o_engagment)
	{
		o_engagment = null;
		ControlPadInput.PadNum result = ControlPadInput.PadNum.Count;
		foreach (KeyValuePair<ControlPadInput.PadNum, ILogicalButton> engagementButton in m_engagementButtons)
		{
			if (engagementButton.Value.JustPressed() && !m_engagedPads.ContainsKey((EngagementSlot)engagementButton.Key))
			{
				result = engagementButton.Key;
			}
		}
		return result;
	}

	protected bool IsEngaged(EngagementSlot _slot)
	{
		return m_engagedPads.ContainsKey(_slot);
	}

	public virtual bool IsBusy()
	{
		return m_engagementRoutines.Count > 0;
	}

	public virtual bool IsWarningActive(PlayerWarning warning)
	{
		if (warning == PlayerWarning.Disengaged && Rejoin.activeInHierarchy)
		{
			return true;
		}
		return false;
	}

	public virtual bool IsEngagingSlot(EngagementSlot slot)
	{
		EngagementRoutine engagementRoutine = m_engagementRoutines.Find((EngagementRoutine x) => x.Slot == slot);
		return engagementRoutine != null;
	}

	protected virtual void Awake()
	{
		m_dimmer.SetActive(false);
		m_rejoin.SetActive(false);
	}

	protected virtual void Start()
	{
		for (int i = 0; i < PlayerInputLookup.GetSystemControllerMaximum(); i++)
		{
			ControlPadInput.PadNum pad = (ControlPadInput.PadNum)i;
			PlayerGameInput playerGameInput = new PlayerGameInput(pad, PadSide.Both, UnsidedAmbiMapping);
			ILogicalButton engagementButton = PlayerInputLookup.GetEngagementButton(playerGameInput);
			m_engagementButtons.Add((ControlPadInput.PadNum)i, engagementButton);
		}
	}

	protected void CallEngagementChangeCallback(EngagementSlot _s, GamepadUser _p, GamepadUser _n)
	{
		this.m_engagementCallback(_s, _p, _n);
	}

	protected virtual bool CanEngage(EngagementSlot _e, ControlPadInput.PadNum _new, bool onlyAllowLostStickyProfiles)
	{
		return true;
	}

	protected void ClearRoutines()
	{
		m_engagementRoutines.Clear();
	}

	protected void AddRoutine(string _routineName, EngagementSlot _slot, IEnumerator _routine)
	{
		m_engagementRoutines.Add(new EngagementRoutine(_slot, _routine));
	}

	protected virtual void Update()
	{
		while (m_engagementRoutines.Count > 0 && !m_engagementRoutines[0].Routine.MoveNext())
		{
			m_engagementRoutines.RemoveAt(0);
		}
	}

	protected void AssignProfileToSlot(EngagementSlot _slot, GamepadUserType _newUser)
	{
		GamepadUserType p = m_engagedPads.SafeGet(_slot, (GamepadUserType)null);
		m_engagedPads.SafeAdd(_slot, _newUser);
		if (m_lostUsers.Contains(_newUser))
		{
			m_lostUsers.Remove(_newUser);
		}
		if (_slot == EngagementSlot.One)
		{
			m_cachedPrimaryUser = _newUser;
		}
		CallEngagementChangeCallback(_slot, p, _newUser);
	}

	protected void RemoveProfileToSlot(EngagementSlot _slot)
	{
		OnPadDisengage(_slot);
		GamepadUserType p = m_engagedPads[_slot];
		m_engagedPads.Remove(_slot);
		CallEngagementChangeCallback(_slot, p, null);
	}

	protected virtual void OnPadDisengage(EngagementSlot _slot)
	{
	}

	protected void DisengageSlots(List<EngagementSlot> _slots)
	{
		for (int i = 0; i < _slots.Count; i++)
		{
			EngagementSlot engagementSlot = _slots[i];
			GamepadUserType val = m_engagedPads[engagementSlot];
			m_engagedPads.Remove(engagementSlot);
			CallEngagementChangeCallback(engagementSlot, val, null);
			bool flag = false;
			if (!ConnectionStatus.IsInSession() || ConnectionStatus.IsHost())
			{
				flag = ServerUserSystem.EngagementsLocked;
			}
			if (val.StickyEngagement)
			{
				if (!m_lostUsers.Contains(val))
				{
					m_lostUsers.Add(val);
				}
				AddRoutine("LostEngagedPlayerRoutine", engagementSlot, LostEngagedPlayerRoutine(engagementSlot));
			}
		}
	}

	protected EngagementSlot GetUnengagedSlot()
	{
		for (int i = 0; i < 4; i++)
		{
			EngagementSlot engagementSlot = (EngagementSlot)i;
			if (!IsEngaged(engagementSlot))
			{
				return engagementSlot;
			}
		}
		return EngagementSlot.Count;
	}

	public virtual bool HasFreeEngagementSlot()
	{
		return GetUnengagedSlot() != EngagementSlot.Count;
	}

	public virtual IEnumerator RunGameownerEngagement(ControlPadInput.PadNum _engagingPadNum, EngagmentCircumstances _circumstances)
	{
		bool itsRunnedMate = false;
		VoidGeneric<GamepadUser> finishedCall = delegate
		{
			itsRunnedMate = true;
		};
		StartGameownerEngagement(_engagingPadNum, _circumstances, finishedCall);
		while (!itsRunnedMate)
		{
			yield return null;
		}
	}

	public virtual void StartGameownerEngagement(ControlPadInput.PadNum _engagingPadNum, EngagmentCircumstances _circumstances, VoidGeneric<GamepadUser> _finishedCall)
	{
		StartPadEngagement(_engagingPadNum, _circumstances, _finishedCall);
	}

	public virtual void StartPadEngagement(ControlPadInput.PadNum _engagingPadNum, EngagmentCircumstances _circumstances, VoidGeneric<GamepadUser> _finishedCall)
	{
		EngagementSlot unengagedSlot = GetUnengagedSlot();
		AddRoutine("PadEngageRoutine", unengagedSlot, PadEngageRoutine(_engagingPadNum, _circumstances, unengagedSlot, _finishedCall));
	}

	protected IEnumerator LostEngagedPlayerRoutine(EngagementSlot _lostPad)
	{
		if (IsEngaged(_lostPad))
		{
			yield break;
		}
		yield return null;
		if (m_rejoinGamerNameText != null)
		{
			if (m_lostUsers.Count > 0)
			{
				GamepadUserType val = m_lostUsers[0];
				if (val.DisplayName.Length <= 20)
				{
					Text rejoinGamerNameText = m_rejoinGamerNameText;
					GamepadUserType val2 = m_lostUsers[0];
					rejoinGamerNameText.text = val2.DisplayName;
				}
				else
				{
					GamepadUserType val3 = m_lostUsers[0];
					string text = val3.DisplayName.Substring(0, 20) + "…";
					m_rejoinGamerNameText.text = text;
				}
			}
			else
			{
				m_rejoinGamerNameText.text = string.Empty;
			}
		}
		Dimmer.SetActive(true);
		Rejoin.SetActive(true);
		TimeManager timeManager = GameUtils.RequestManager<TimeManager>();
		m_lostEngagedPauseLayer = ((!ConnectionStatus.IsInSession()) ? TimeManager.PauseLayer.System : TimeManager.PauseLayer.Network);
		if (timeManager != null)
		{
			timeManager.SetPaused(m_lostEngagedPauseLayer.Value, true, this);
		}
		T17EventSystem eventSystem = T17EventSystemsManager.Instance.GetEventSystemForEngagementSlot(EngagementSlot.One);
		if (eventSystem != null)
		{
			m_lostEngagedSuppressor = eventSystem.Disable(this);
		}
		T17EventSystemsManager.Instance.DisableAllEventSystemsExceptFor(null);
		GamepadUser engagedUser = null;
		while (engagedUser == null)
		{
			ControlPadInput.PadNum engagingPad = ControlPadInput.PadNum.Count;
			EngagmentCircumstances circumstances = null;
			while (engagingPad == ControlPadInput.PadNum.Count)
			{
				yield return null;
				if (GetUser(_lostPad) != null)
				{
					break;
				}
				engagingPad = GetEngagementPad(out circumstances);
				if (engagingPad != ControlPadInput.PadNum.Count && !CanEngage(_lostPad, engagingPad, true))
				{
					engagingPad = ControlPadInput.PadNum.Count;
				}
			}
			if (m_engagementButtons.ContainsKey(engagingPad))
			{
				while (m_engagementButtons[engagingPad].IsDown())
				{
					yield return null;
				}
			}
			IEnumerator login = PadEngageRoutine(engagingPad, circumstances, _lostPad, false, null);
			while (login.MoveNext())
			{
				yield return null;
			}
			engagedUser = GetUser(_lostPad);
		}
		FinishLostPadEngagement();
	}

	private void FinishLostPadEngagement()
	{
		T17EventSystemsManager.Instance.EnableAllEventSystems();
		if (m_lostEngagedSuppressor != null)
		{
			m_lostEngagedSuppressor.Release();
		}
		TimeManager timeManager = GameUtils.RequestManager<TimeManager>();
		if (timeManager != null && m_lostEngagedPauseLayer.HasValue)
		{
			if (!TimeManager.IsPaused(m_lostEngagedPauseLayer.Value))
			{
				timeManager.SetPaused(m_lostEngagedPauseLayer.Value, true, this);
			}
			timeManager.SetPaused(m_lostEngagedPauseLayer.Value, false, this);
		}
		m_lostEngagedPauseLayer = null;
		Dimmer.SetActive(false);
		Rejoin.SetActive(false);
	}

	public virtual void DisengagePad(EngagementSlot _intendedSlot)
	{
		if (IsEngaged(_intendedSlot))
		{
			RemoveProfileToSlot(_intendedSlot);
		}
	}

	protected virtual IEnumerator PadEngageRoutine(ControlPadInput.PadNum _engagingPadNum, EngagmentCircumstances _circumstances, EngagementSlot _intendedSlot, VoidGeneric<GamepadUser> _finishedCallback)
	{
		return PadEngageRoutine(_engagingPadNum, _circumstances, _intendedSlot, false, _finishedCallback);
	}

	protected abstract IEnumerator PadEngageRoutine(ControlPadInput.PadNum _engagingPadNum, EngagmentCircumstances _circumstances, EngagementSlot _intendedSlot, bool _forceUserChoice, VoidGeneric<GamepadUser> _finishedCallback);

	public abstract bool HasPlayer();

	public abstract bool HasSavablePlayer();

	public virtual void ShowGamerCard(EngagementSlot slot)
	{
	}

	public virtual void ShowGamerCard(GamepadUser localUser)
	{
	}

	public virtual void ShowGamerCard(OnlineUserPlatformId onlineUser)
	{
	}

	public abstract bool SupportsInvitesForAnyUser();
}
