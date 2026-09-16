namespace GameModes
{
	public struct ClientSetupData
	{
		public IClientRoundTimer m_roundTimer;

		public ClientOrderControllerBuilder m_orderControllerBuilder;

		public OnSessionConfigChanged m_onSessionConfigChangedCallback;

		public OnOrderAddedClient m_onOrderAdded;

		public OnOrderExpiredClient m_onOrderExpired;

		public OnSuccessfulDeliveryClient m_onSuccessfulDelivery;

		public OnFailedDeliveryClient m_onFailedDelivery;

		public OnOutroClient m_onOutro;
	}
}
