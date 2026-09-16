using System;
using System.Collections;
using System.Collections.Generic;
using Team17.Online;
using UnityEngine;

[ExecutionDependency(typeof(MultiChefPadUIController))]
public class MultiChefUIController : UIControllerBase
{
	private class PadPlayerInfo
	{
		public PlayerInputLookup.Player PlayerId;

		public ControlPadInput.PadNum Pad;

		public PadSide PadSide;

		public PadPlayerInfo(PlayerInputLookup.Player _p, ControlPadInput.PadNum _n, PadSide _s)
		{
			PlayerId = _p;
			Pad = _n;
			PadSide = _s;
		}
	}

	[SerializeField]
	[AssignComponentRecursive(Editorbility.NonEditable)]
	private MultiChefPadUIController[] m_padUI = new MultiChefPadUIController[4];

	[SerializeField]
	[AssignResource("SidedAmbiControlsMappingData", Editorbility.NonEditable)]
	private AmbiControlsMappingData m_sidedMappingData;

	[SerializeField]
	[AssignResource("UnsidedAmbiControlsMappingData", Editorbility.NonEditable)]
	private AmbiControlsMappingData m_unsidedMappingData;

	private ILogicalButton m_completeButton;

	private Dictionary<ControlPadInput.PadNum, MultiChefPadUIController> m_padUIDictionary = new Dictionary<ControlPadInput.PadNum, MultiChefPadUIController>();

	public event CallbackVoid OnExitCallback = delegate
	{
	};

	private void Awake()
	{
		for (int i = 0; i < m_padUI.Length; i++)
		{
			m_padUIDictionary.Add(m_padUI[i].PadNum, m_padUI[i]);
			m_padUI[i].Reinitialise();
		}
		for (int j = 0; j < m_padUI.Length; j++)
		{
			m_padUI[j].CanAddPlayerQuery = CanAddPlayer;
			m_padUI[j].OnSplitStateChange += OnSplitChange;
		}
		ILogicalButton uIButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UICancel);
		ILogicalButton uIButton2 = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UISelectNotStart);
		m_completeButton = new ComboLogicalButton(new ILogicalButton[2] { uIButton, uIButton2 });
	}

	private void Update()
	{
		if (m_completeButton.JustPressed() && !TimeManager.IsPaused(TimeManager.PauseLayer.System))
		{
			this.OnExitCallback();
		}
	}

	public bool CanAddPlayer(int _numToAdd)
	{
		int num = 0;
		foreach (object item in IteratePadPlayerAssignment())
		{
			num++;
		}
		return num + _numToAdd <= m_padUIDictionary.Count;
	}

	private IEnumerable IteratePadPlayerAssignment()
	{
		int playerId = 0;
		for (int i = 0; i < m_padUIDictionary.Count; i++)
		{
			ControlPadInput.PadNum padNum = (ControlPadInput.PadNum)i;
			MultiChefPadUIController padUIController = m_padUIDictionary[padNum];
			IEnumerator enumerator = padUIController.IterateSides().GetEnumerator();
			try
			{
				while (enumerator.MoveNext())
				{
					PadSide side = (PadSide)enumerator.Current;
					yield return new PadPlayerInfo((PlayerInputLookup.Player)playerId, padNum, side);
					playerId++;
				}
			}
			finally
			{
				IDisposable disposable2;
				IDisposable disposable = (disposable2 = enumerator as IDisposable);
				if (disposable2 != null)
				{
					disposable.Dispose();
				}
			}
		}
	}

	private void OnSplitChange()
	{
		List<GameInputConfig.ConfigEntry> list = new List<GameInputConfig.ConfigEntry>();
		User.MachineID s_LocalMachineId = ClientUserSystem.s_LocalMachineId;
		foreach (PadPlayerInfo item in IteratePadPlayerAssignment())
		{
			MultiChefPadUIController multiChefPadUIController = m_padUIDictionary[item.Pad];
			multiChefPadUIController.AssignPlayer(item.PadSide, item.PlayerId);
			AmbiControlsMappingData mappingData = ((item.PadSide != PadSide.Both) ? m_sidedMappingData : m_unsidedMappingData);
			list.Add(new GameInputConfig.ConfigEntry(item.PlayerId, item.Pad, item.PadSide, s_LocalMachineId, mappingData));
		}
		PadSplitManager.FixupConfigList(list, m_unsidedMappingData);
		GameInputConfig baseInputConfig = new GameInputConfig(list.ToArray());
		PlayerInputLookup.SetBaseInputConfig(baseInputConfig);
	}
}
