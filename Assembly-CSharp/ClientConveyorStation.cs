using System;
using Team17.Online.Multiplayer.Messaging;

public class ClientConveyorStation : ClientSynchroniserBase
{
	public ConveyorStation m_conveyorStation;

	public static CreateClientSidePredictionCallback m_CreateConveyorPredictionCallback = CreateConveyorPrediction;

	private IClientAttachment m_item;

	private IClientConveyenceReceiver m_receiver;

	private uint m_receiverEntityID;

	private bool m_isConveying;

	private CallbackBool m_conveyStateChanged = delegate
	{
	};

	public static IClientSidePredicted CreateConveyorPrediction()
	{
		return new ConveyorPrediction();
	}

	public override EntityType GetEntityType()
	{
		return EntityType.ConveyorStation;
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		ConveyorStationMessage conveyorStationMessage = (ConveyorStationMessage)serialisable;
		EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(conveyorStationMessage.m_receiverEntityID);
		EntitySerialisationEntry entry2 = EntitySerialisationRegistry.GetEntry(conveyorStationMessage.m_itemEntityID);
		if (entry2 == null)
		{
			return;
		}
		IClientConveyenceReceiver clientConveyenceReceiver = entry.m_GameObject.RequireInterface<IClientConveyenceReceiver>();
		if (clientConveyenceReceiver != m_receiver)
		{
			m_receiver = clientConveyenceReceiver;
			m_receiverEntityID = conveyorStationMessage.m_receiverEntityID;
		}
		m_item = entry2.m_GameObject.RequireInterface<IClientAttachment>();
		if (m_item == null)
		{
			return;
		}
		IParentable parentable = entry.m_GameObject.RequestInterface<IParentable>();
		if (parentable == null)
		{
			return;
		}
		IClientSidePredicted clientSidePrediction = m_item.GetClientSidePrediction();
		if (clientSidePrediction == null || !(clientSidePrediction is ConveyorPrediction))
		{
			m_item.SetClientSidePrediction(m_CreateConveyorPredictionCallback);
			clientSidePrediction = m_item.GetClientSidePrediction();
			ConveyorPrediction conveyorPrediction = clientSidePrediction as ConveyorPrediction;
			if (conveyorPrediction != null)
			{
				conveyorPrediction.m_Transform = m_item.AccessGameObject().transform;
			}
		}
		ConveyorPrediction conveyorPrediction2 = clientSidePrediction as ConveyorPrediction;
		if (conveyorPrediction2 != null)
		{
			ConveyorPrediction.Destination dest = new ConveyorPrediction.Destination
			{
				targetEntityID = conveyorStationMessage.m_receiverEntityID,
				targetTransform = parentable.GetAttachPoint(m_item.AccessGameObject()),
				arriveTime = conveyorStationMessage.m_arriveTime
			};
			conveyorPrediction2.EnqueueDestination(dest);
		}
	}

	private void OnStartedMovingToDestination(ConveyorPrediction.Destination destination, ConveyorPrediction prediction)
	{
		if (!m_isConveying)
		{
			m_isConveying = true;
			m_conveyStateChanged(m_isConveying);
		}
		if (m_receiverEntityID == destination.targetEntityID)
		{
			m_receiver.InformStartingConveyToMe();
		}
	}

	private void OnDestinationReached(ConveyorPrediction.Destination destination, ConveyorPrediction prediction)
	{
		if (m_receiverEntityID == destination.targetEntityID)
		{
			m_item = null;
			if (m_isConveying)
			{
				m_isConveying = false;
				m_conveyStateChanged(m_isConveying);
			}
			if (m_receiver != null)
			{
				m_receiver.InformEndingConveyToMe();
			}
		}
	}

	private void Awake()
	{
		m_conveyorStation = base.gameObject.RequireComponent<ConveyorStation>();
		ConveyorPrediction.OnStartedMovingToDestination = (GenericVoid<ConveyorPrediction.Destination, ConveyorPrediction>)Delegate.Combine(ConveyorPrediction.OnStartedMovingToDestination, new GenericVoid<ConveyorPrediction.Destination, ConveyorPrediction>(OnStartedMovingToDestination));
		ConveyorPrediction.OnDestinationReached = (GenericVoid<ConveyorPrediction.Destination, ConveyorPrediction>)Delegate.Combine(ConveyorPrediction.OnDestinationReached, new GenericVoid<ConveyorPrediction.Destination, ConveyorPrediction>(OnDestinationReached));
	}

	protected override void OnDestroy()
	{
		ConveyorPrediction.OnStartedMovingToDestination = (GenericVoid<ConveyorPrediction.Destination, ConveyorPrediction>)Delegate.Remove(ConveyorPrediction.OnStartedMovingToDestination, new GenericVoid<ConveyorPrediction.Destination, ConveyorPrediction>(OnStartedMovingToDestination));
		ConveyorPrediction.OnDestinationReached = (GenericVoid<ConveyorPrediction.Destination, ConveyorPrediction>)Delegate.Remove(ConveyorPrediction.OnDestinationReached, new GenericVoid<ConveyorPrediction.Destination, ConveyorPrediction>(OnDestinationReached));
		base.OnDestroy();
	}

	public bool IsConveying()
	{
		return m_isConveying;
	}

	public void RegisterConveyStateChangedCallback(CallbackBool _callback)
	{
		m_conveyStateChanged = (CallbackBool)Delegate.Combine(m_conveyStateChanged, _callback);
	}

	public void UnregisterConveyStateChangedCallback(CallbackBool _callback)
	{
		m_conveyStateChanged = (CallbackBool)Delegate.Remove(m_conveyStateChanged, _callback);
	}
}
