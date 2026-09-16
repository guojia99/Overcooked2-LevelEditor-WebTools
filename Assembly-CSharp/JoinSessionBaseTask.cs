using System.ComponentModel;
using BitStream;
using Team17.Online;

public abstract class JoinSessionBaseTask : IMultiplayerTask
{
	public class UserData
	{
		[DefaultValue(null)]
		public OnlineMultiplayerLocalUserId UserId { get; set; }

		[DefaultValue(EngagementSlot.Count)]
		public EngagementSlot Slot { get; set; }
	}

	protected JoinSessionStatus m_Status = new JoinSessionStatus();

	protected JoinData m_JoinData = new JoinData();

	protected bool m_JoinDataReceived;

	public abstract void TryStart();

	public virtual void Start(object startData)
	{
		m_JoinDataReceived = false;
		m_Status.Progress = eConnectionModeSwitchProgress.NotStarted;
		m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
		TryStart();
	}

	public void Stop()
	{
		m_Status.Progress = eConnectionModeSwitchProgress.NotStarted;
		m_Status.Result = eConnectionModeSwitchResult.NotAvailableYet;
	}

	public virtual void Update()
	{
		if (m_Status.Progress == eConnectionModeSwitchProgress.NotStarted && m_Status.Result == eConnectionModeSwitchResult.NotAvailableYet)
		{
			TryStart();
		}
	}

	public object GetData()
	{
		if (m_JoinDataReceived)
		{
			return m_JoinData;
		}
		return null;
	}

	public IConnectionModeSwitchStatus GetStatus()
	{
		return m_Status;
	}

	public void OnlineMultiplayerSessionJoinCallback(OnlineMultiplayerReturnCode<OnlineMultiplayerSessionJoinResult> result, byte[] replyData, int replyDataSize)
	{
		m_Status.sessionJoinResult = result;
		if (result != null && result.m_returnCode == OnlineMultiplayerSessionJoinResult.eSuccess)
		{
			if (replyData != null)
			{
				BitStreamReader bitStreamReader = new BitStreamReader(replyData);
				m_JoinData.machine = (User.MachineID)bitStreamReader.ReadUInt32(3);
				m_JoinData.timeSync.Deserialise(bitStreamReader);
				m_JoinData.usersChanged.Deserialise(bitStreamReader);
				m_JoinData.gameSetup.Deserialise(bitStreamReader);
				m_JoinDataReceived = true;
			}
			else
			{
				m_JoinDataReceived = false;
			}
			m_Status.Result = eConnectionModeSwitchResult.Success;
		}
		else
		{
			m_Status.Result = eConnectionModeSwitchResult.Failure;
		}
		m_Status.Progress = eConnectionModeSwitchProgress.Complete;
	}
}
