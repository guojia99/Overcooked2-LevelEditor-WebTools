using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerPlateStation : ServerSynchroniserBase
{
	private PlateStation m_plateStation;

	private PlateStationMessage m_data = new PlateStationMessage();

	private ServerPlateReturnStation[] m_serverReturnStations = new ServerPlateReturnStation[0];

	private IKitchenOrderHandler m_orderHandler;

	private ServerAttachStation m_attachStation;

	private ServerPlate m_plate;

	public override EntityType GetEntityType()
	{
		return EntityType.PlateStation;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_orderHandler = GameUtils.RequireManagerInterface<IKitchenOrderHandler>();
		m_attachStation = base.gameObject.GetComponent<ServerAttachStation>();
		m_attachStation.RegisterOnItemAdded(OnItemAdded);
		m_attachStation.RegisterOnItemRemoved(OnItemRemoved);
		m_attachStation.RegisterAllowItemPlacement(CanAddItem);
		m_attachStation.RegisterFailedToPlace(OnFailedToPlace);
		m_plateStation = (PlateStation)synchronisedObject;
		if (m_plateStation.m_returnStations != null && m_plateStation.m_returnStations.Length > 0)
		{
			m_serverReturnStations = m_plateStation.m_returnStations.ConvertAll((PlateReturnStation x) => x.gameObject.RequireComponent<ServerPlateReturnStation>());
		}
	}

	private void SendDeliverEvent(GameObject _object, bool _success)
	{
		m_data.m_delivered = _object;
		m_data.m_success = _success;
		SendServerEvent(m_data);
	}

	public Transform GetAttachPoint(GameObject gameObject)
	{
		return m_attachStation.GetAttachPoint(gameObject);
	}

	public ServerPlateReturnStation GetReturnStation(PlatingStepData _plateType)
	{
		int num = m_serverReturnStations.FindIndex_Predicate((ServerPlateReturnStation x) => x.GetPlatingStep() == _plateType);
		if (num >= 0)
		{
			return m_serverReturnStations[num];
		}
		return null;
	}

	public TeamID GetTeamID()
	{
		return m_plateStation.m_teamId;
	}

	private void DeliverCurrentPlate()
	{
		ServerPlate plate = m_plate;
		plate.Reserve(this);
		m_orderHandler.FoodDelivered(plate.GetOrderComposition(), plate.GetPlatingStep(), this);
		plate.StartDeliverySequence(this);
		SendDeliverEvent(plate.gameObject, true);
	}

	private bool CanAddItem(GameObject _object, PlacementContext _context)
	{
		if (m_plate == null)
		{
			return _object.GetComponent<ServerPlate>() != null;
		}
		return true;
	}

	private void OnItemAdded(IAttachment _iHoldable)
	{
		m_plate = _iHoldable.AccessGameObject().GetComponent<ServerPlate>();
		ServerIngredientContainer component = m_plate.GetComponent<ServerIngredientContainer>();
		if (!(component != null))
		{
			return;
		}
		IIngredientContents component2 = component.GetComponent<IIngredientContents>();
		if (component2 != null && component2.GetContentsCount() >= 1)
		{
			AssembledDefinitionNode[] contents = component2.GetContents();
			for (int i = 0; i < contents.Length; i++)
			{
				if (contents[i] != null && contents[i].m_freeObject != null)
				{
					NetworkUtils.DestroyObject(contents[i].m_freeObject);
				}
			}
		}
		DeliverCurrentPlate();
	}

	private void OnItemRemoved(IAttachment _iHoldable)
	{
		m_plate = null;
	}

	private void OnFailedToPlace(GameObject _object)
	{
		SendDeliverEvent(_object, false);
	}

	private void Awake()
	{
	}
}
