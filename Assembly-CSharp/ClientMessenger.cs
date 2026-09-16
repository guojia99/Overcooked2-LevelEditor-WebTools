using Team17.Online;
using Team17.Online.Multiplayer;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

internal class ClientMessenger
{
	private static ExampleNetworkMessage m_Example = new ExampleNetworkMessage();

	private static GameStateMessage m_GameState = new GameStateMessage();

	private static ChefAvatarMessage m_ChefAvatar = new ChefAvatarMessage();

	private static ChefEventMessage m_ChefEvent = new ChefEventMessage();

	public static MapAvatarHornMessage m_mapAvatarMessage = new MapAvatarHornMessage();

	public static ResumeEntitySyncMessage m_resumeEntitySyncMessage = new ResumeEntitySyncMessage();

	private static ControllerSettingsMessage m_ControllerSettings = new ControllerSettingsMessage();

	private static HighScoresMessage m_HighScores = new HighScoresMessage();

	private static Client m_LocalClient = null;

	public static void ControllerState(ControllerStateMessage _controllerState)
	{
		m_LocalClient.SendMessageToServer(MessageType.Input, _controllerState);
	}

	public static void Example(float fFloat, bool bBool)
	{
		m_Example.Initialise(fFloat, bBool);
		m_LocalClient.SendMessageToServer(MessageType.Example, m_Example);
	}

	public static void GameState(GameState state)
	{
		m_GameState.Initialise(state, ClientUserSystem.s_LocalMachineId);
		m_LocalClient.SendMessageToServer(MessageType.GameState, m_GameState);
	}

	public static void ChefAvatar(uint chefAvatar, User user)
	{
		m_ChefAvatar.Initialise(chefAvatar, user.Machine, user.Engagement, user.Split);
		m_LocalClient.SendMessageToServer(MessageType.ChefAvatar, m_ChefAvatar);
	}

	public static void ChefEventMessage(ChefEventMessage.ChefEventType _type, GameObject _chef, MonoBehaviour _object)
	{
		uint entityID = 0u;
		if (_object != null)
		{
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(_object.gameObject);
			if (entry != null)
			{
				entityID = entry.m_Header.m_uEntityID;
			}
		}
		uint chefEntityID = 0u;
		EntitySerialisationEntry entry2 = EntitySerialisationRegistry.GetEntry(_chef);
		if (entry2 != null)
		{
			chefEntityID = entry2.m_Header.m_uEntityID;
		}
		m_ChefEvent.Initialise(_type, chefEntityID, entityID);
		m_LocalClient.SendMessageToServer(MessageType.ChefEvent, m_ChefEvent);
	}

	public static void ChefEventMessage(ChefEventMessage.ChefEventType _type, GameObject _chef, GameObject _object)
	{
		uint entityID = 0u;
		if (_object != null)
		{
			EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(_object);
			if (entry != null)
			{
				entityID = entry.m_Header.m_uEntityID;
			}
		}
		EntitySerialisationEntry entry2 = EntitySerialisationRegistry.GetEntry(_chef);
		if (entry2 != null)
		{
			uint uEntityID = entry2.m_Header.m_uEntityID;
			m_ChefEvent.Initialise(_type, uEntityID, entityID);
			m_LocalClient.SendMessageToServer(MessageType.ChefEvent, m_ChefEvent);
		}
	}

	public static void ChefEventMessage(ChefEventMessage.ChefEventType _type, GameObject _chef)
	{
		uint entityID = 0u;
		EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(_chef);
		uint uEntityID = entry.m_Header.m_uEntityID;
		m_ChefEvent.Initialise(_type, uEntityID, entityID);
		m_LocalClient.SendMessageToServer(MessageType.ChefEvent, m_ChefEvent);
	}

	public static void ChefKnockbackEventMessage(ChefEventMessage.KnockbackType _knockbackType, uint _chef, Vector2 _knockbackForce, Vector3 _relativeContactPoint)
	{
		uint entityID = 0u;
		m_ChefEvent.Initialise(Team17.Online.Multiplayer.Messaging.ChefEventMessage.ChefEventType.KnockBack, _chef, entityID);
		m_ChefEvent.Knockback_Type = _knockbackType;
		m_ChefEvent.KnockbackForce = _knockbackForce;
		m_ChefEvent.RelativeContactPoint = _relativeContactPoint;
		m_LocalClient.SendMessageToServer(MessageType.ChefEvent, m_ChefEvent);
	}

	public static void LobbyMessage(LobbyClientMessage _message)
	{
		m_LocalClient.SendMessageToServer(MessageType.LobbyClient, _message);
	}

	public static void MapAvatarHorn(int _playerIdx)
	{
		m_mapAvatarMessage.m_playerIdx = _playerIdx;
		m_LocalClient.SendMessageToServer(MessageType.MapAvatarHorn, m_mapAvatarMessage);
	}

	public static void EmoteWheelMessage(EmoteWheelMessage _message)
	{
		m_LocalClient.SendMessageToServer(MessageType.EmoteWheel, _message);
	}

	public static void SendResumeEntitySync(GameObject go)
	{
		EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(go);
		m_resumeEntitySyncMessage.Initialise(entry.m_Header);
		m_LocalClient.SendMessageToServer(MessageType.ResumeEntitySync, m_resumeEntitySyncMessage);
	}

	public static void ControllerSettings(PadSide side, User user)
	{
		m_ControllerSettings.Initialise(side, user.Machine, user.Engagement, user.Split);
		m_LocalClient.SendMessageToServer(MessageType.ControllerSettings, m_ControllerSettings);
	}

	public static void HighScores(GameProgress.HighScores highScores, int DLC)
	{
		m_HighScores.HighScores = highScores;
		m_HighScores.DLC = DLC;
		m_HighScores.m_Machine = ClientUserSystem.s_LocalMachineId;
		m_LocalClient.SendMessageToServer(MessageType.HighScores, m_HighScores);
	}

	public static void OnClientStarted(Client client)
	{
		m_LocalClient = client;
	}
}
