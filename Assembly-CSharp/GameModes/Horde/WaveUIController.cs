using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

namespace GameModes.Horde
{
	public class WaveUIController : UIControllerBase
	{
		[SerializeField]
		private string m_waveNumberUILocalisationTag = "Horde.Wave";

		[SerializeField]
		private T17Text m_waveText;

		private ClientHordeFlowController m_flowController;

		private int m_waveCount = 1;

		private void Awake()
		{
			HordeLevelConfig hordeLevelConfig = GameUtils.GetLevelConfig() as HordeLevelConfig;
			m_waveCount = hordeLevelConfig.m_waves.Count;
			string nonLocalizedText = Localization.Get(m_waveNumberUILocalisationTag, new LocToken("[Number]", "1"), new LocToken("[NumberMax]", m_waveCount.ToString()));
			m_waveText.SetNonLocalizedText(nonLocalizedText);
			Mailbox.Client.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
		}

		private void OnDestroy()
		{
			Mailbox.Client.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
		}

		private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
		{
			GameStateMessage gameStateMessage = (GameStateMessage)message;
			if (gameStateMessage.m_State == GameState.StartEntities)
			{
				FlowControllerBase flowControllerBase = GameUtils.RequireManager<FlowControllerBase>();
				m_flowController = flowControllerBase.gameObject.RequireComponent<ClientHordeFlowController>();
				m_flowController.RegisterOnBeginWave(this, OnBeginWave);
			}
		}

		private void OnBeginWave(ClientHordeFlowController flowController, int waveNumber)
		{
			string nonLocalizedText = Localization.Get(m_waveNumberUILocalisationTag, new LocToken("[Number]", waveNumber.ToString()), new LocToken("[NumberMax]", m_waveCount.ToString()));
			m_waveText.SetNonLocalizedText(nonLocalizedText);
		}
	}
}
