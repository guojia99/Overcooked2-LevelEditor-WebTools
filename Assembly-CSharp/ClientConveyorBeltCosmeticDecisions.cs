using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientConveyorBeltCosmeticDecisions : ClientSynchroniserBase
{
	public bool LastScrollSet = true;

	public float m_ScrollSpeed = 0.28f;

	public float ConveySpeedToUVSpeed = 0.56f;

	private MeshRenderer m_targetRenderer;

	private IClientConveyenceReceiver m_receiver;

	private ClientConveyorStation m_station;

	private ClientAttachStation m_attacher;

	private MaterialPropertyBlock m_materialPropertyBlock;

	private ConveyorStation m_conveyorStation;

	private int m_speedPropID;

	protected virtual void Awake()
	{
		m_speedPropID = Shader.PropertyToID("_speed");
		m_materialPropertyBlock = new MaterialPropertyBlock();
		m_conveyorStation = GetComponent<ConveyorStation>();
		m_ScrollSpeed = ConveySpeedToUVSpeed * m_conveyorStation.m_conveySpeed;
	}

	protected virtual void Start()
	{
		m_materialPropertyBlock.SetFloat(m_speedPropID, (!LastScrollSet) ? 0f : m_ScrollSpeed);
		m_targetRenderer = m_conveyorStation.m_targetRenderer;
		m_targetRenderer.SetPropertyBlock(m_materialPropertyBlock);
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_station = base.gameObject.RequireComponent<ClientConveyorStation>();
		m_attacher = base.gameObject.RequireComponent<ClientAttachStation>();
		m_receiver = base.gameObject.RequireInterface<IClientConveyenceReceiver>();
		m_station.RegisterConveyStateChangedCallback(delegate
		{
			OnStateChanged();
		});
		m_attacher.RegisterOnItemAdded(delegate
		{
			OnStateChanged();
		});
		m_attacher.RegisterOnItemRemoved(delegate
		{
			OnStateChanged();
		});
		m_receiver.RegisterRefreshedConveyToCallback(OnStateChanged);
		OnStateChanged();
	}

	private void OnStateChanged()
	{
		bool flag = m_station.IsConveying() || m_receiver.IsReceiving() || !m_attacher.HasItem();
		if (flag != LastScrollSet)
		{
			LastScrollSet = flag;
			m_materialPropertyBlock.SetFloat(m_speedPropID, (!flag) ? 0f : m_ScrollSpeed);
			m_targetRenderer.SetPropertyBlock(m_materialPropertyBlock);
		}
	}
}
