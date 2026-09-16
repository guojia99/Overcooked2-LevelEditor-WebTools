using Team17.Online;
using UnityEngine;

[RequireComponent(typeof(PersistentObject))]
public class LobbySetupInfo : MonoBehaviour
{
	public static LobbySetupInfo Instance;

	public const string LobbyScene = "Lobbies";

	private PersistentObject m_persist;

	public OnlineMultiplayerConnectionMode m_connectionMode;

	public OnlineMultiplayerSessionVisibility m_visiblity = OnlineMultiplayerSessionVisibility.eClosed;

	public GameSession.GameType m_gameType;

	public NetConnectionState m_originalConnectionState;

	private void Awake()
	{
		if (Instance != null)
		{
			Object.Destroy(base.gameObject);
			return;
		}
		Instance = this;
		m_persist = base.gameObject.RequireComponent<PersistentObject>();
		m_persist.m_defaultBehaviour = PersistentObject.PersistType.DontPersist;
		m_persist.AddPersistingLevel("Lobbies");
		m_persist.AddPersistingLevel("Loading");
	}

	private void OnDestroy()
	{
		if (Instance == this)
		{
			Object.Destroy(base.gameObject);
			Instance = null;
		}
	}
}
