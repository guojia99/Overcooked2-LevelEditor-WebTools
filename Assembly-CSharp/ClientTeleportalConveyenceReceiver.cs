using Team17.Online.Multiplayer.Messaging;

public class ClientTeleportalConveyenceReceiver : ClientSynchroniserBase, IClientConveyenceReceiver
{
	private TeleportalConveyenceReceiver m_teleportalConveyenceReceiver;

	private bool m_receiving;

	private void Awake()
	{
		m_teleportalConveyenceReceiver = base.gameObject.RequireComponent<TeleportalConveyenceReceiver>();
	}

	public void InformStartingConveyToMe()
	{
		m_receiving = true;
	}

	public void InformEndingConveyToMe()
	{
		m_receiving = false;
	}

	public bool IsReceiving()
	{
		return m_receiving;
	}

	public void RefreshConveyTo()
	{
	}

	public void RegisterRefreshedConveyToCallback(CallbackVoid _callback)
	{
	}

	public void UnregisterRefreshedConveyToCallback(CallbackVoid _callback)
	{
	}
}
