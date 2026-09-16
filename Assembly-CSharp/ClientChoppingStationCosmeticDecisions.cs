using Team17.Online.Multiplayer.Messaging;

public class ClientChoppingStationCosmeticDecisions : ClientSynchroniserBase
{
	private ChoppingStationCosmeticDecisions m_choppingStationCosmeticDecisions;

	private ClientAttachStation m_attachStation;

	private void Awake()
	{
		m_choppingStationCosmeticDecisions = base.gameObject.RequireComponent<ChoppingStationCosmeticDecisions>();
		m_attachStation = base.gameObject.RequireComponent<ClientAttachStation>();
		m_attachStation.RegisterOnItemAdded(OnItemAdded);
		m_attachStation.RegisterOnItemRemoved(OnItemRemoved);
	}

	private void OnItemAdded(IClientAttachment _iHoldable)
	{
		m_choppingStationCosmeticDecisions.m_knifeModel.SetActive(false);
	}

	private void OnItemRemoved(IClientAttachment _iHoldable)
	{
		m_choppingStationCosmeticDecisions.m_knifeModel.SetActive(true);
	}
}
