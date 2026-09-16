namespace GameModes
{
	public struct ServerSetupData
	{
		public IServerRoundTimer m_roundTimer;

		public ServerOrderControllerBuilder m_orderControllerBuilder;

		public OnSessionConfigChanged m_onSessionConfigChangedCallback;

		public OnOrderAddedServer m_onOrderAdded;

		public OnOrderExpiredServer m_onOrderExpired;

		public OnSuccessfulDeliveryServer m_onSuccessfulDelivery;

		public OnFailedDeliveryServer m_onFailedDelivery;

		public OnOutroSceneServer m_onOutroScene;
	}
}
