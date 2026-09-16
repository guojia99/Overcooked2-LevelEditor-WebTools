using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class OvercookedAchievementManager : SteamAchievementManager
{
	[SerializeField]
	private StatsTracking[] m_StatsTracking;

	private bool m_initialised;

	protected override void Awake()
	{
		base.Awake();
		SetupStatSystem();
	}

	private void SetupStatSystem()
	{
		m_StatSystem.Clear();
		for (int i = 0; i < m_StatsTracking.Length; i++)
		{
			m_StatSystem.Setup(m_StatsTracking[i]);
		}
		m_StatSystem.SetupDone();
	}

	public override void Init()
	{
		if (!m_initialised)
		{
			m_initialised = true;
			m_StatSystem.LoadStats();
			Mailbox.Client.RegisterForMessageType(MessageType.Achievement, OnAchievementRecieved);
			m_StatSystem.RegisterTrophyProgress(OnTrophyProgress);
			m_StatSystem.RegisterTrophyUnlock(OnTrophyUnlock);
			base.Init();
		}
	}

	public override void Unload()
	{
		base.Unload();
		m_initialised = false;
		Mailbox.Client.UnregisterForMessageType(MessageType.Achievement, OnAchievementRecieved);
		m_StatSystem.UnregisterTrophyProgress(OnTrophyProgress);
		m_StatSystem.UnregisterTrophyUnlock(OnTrophyUnlock);
		SetupStatSystem();
	}

	protected override void OnDestroy()
	{
		Unload();
		base.OnDestroy();
	}

	private void OnTrophyProgress(int trophyId, float progress)
	{
		SetProgress(trophyId, progress);
	}

	private void OnTrophyUnlock(int trophyId)
	{
		Unlock(trophyId);
	}

	public void IncStat(int ID, float value, ControlPadInput.PadNum pad)
	{
		m_StatSystem.IncStat(ID, value, pad);
	}

	public void AddIDStat(int ID, int itemID, ControlPadInput.PadNum pad)
	{
		m_StatSystem.AddIDStat(ID, itemID, pad);
	}

	private void OnAchievementRecieved(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		AchievementMessage achievementMessage = (AchievementMessage)message;
		GameObject gameObject = EntitySerialisationRegistry.GetEntry(achievementMessage.m_Header.m_uEntityID).m_GameObject;
		if (gameObject != null)
		{
			PlayerIDProvider playerIDProvider = gameObject.RequestComponent<PlayerIDProvider>();
			if (playerIDProvider != null)
			{
				ControlPadInput.PadNum padForPlayer = PlayerInputLookup.GetPadForPlayer(playerIDProvider.GetID());
				IncStat(achievementMessage.m_statId, achievementMessage.m_increment, padForPlayer);
			}
		}
	}
}
