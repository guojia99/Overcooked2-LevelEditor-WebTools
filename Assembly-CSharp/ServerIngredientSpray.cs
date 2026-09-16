using System.Collections.Generic;
using UnityEngine;

public class ServerIngredientSpray : ServerSprayingUtensil
{
	private struct SprayedInfo
	{
		public const float kCOOLDOWN_TIME = 0.25f;

		public IHandlePlacement m_IPlacement;

		public float m_CooldownTimer;

		public SprayedInfo(IHandlePlacement _iPlacement)
		{
			m_IPlacement = _iPlacement;
			m_CooldownTimer = 0.25f;
		}
	}

	public class CarrierAdapter : ICarrier, ICarrierPlacement
	{
		private ServerIngredientSpray m_Spray;

		public CarrierAdapter(ServerIngredientSpray _spray)
		{
			m_Spray = _spray;
		}

		public GameObject InspectCarriedItem()
		{
			return m_Spray.m_Spray.m_OrderPrefab;
		}

		public GameObject AccessGameObject()
		{
			return m_Spray.Carrier.gameObject;
		}

		public void RegisterCarriedItemChangeCallback(VoidGeneric<GameObject, GameObject> _callback)
		{
		}

		public void UnregisterCarriedItemChangeCallback(VoidGeneric<GameObject, GameObject> _callback)
		{
		}

		public void CarryItem(GameObject _object)
		{
		}

		public GameObject TakeItem()
		{
			return null;
		}

		public void DestroyCarriedItem()
		{
		}
	}

	public IngredientSpray m_Spray;

	private CarrierAdapter m_Adapter;

	private PlacementContext m_PlacementContext = new PlacementContext(PlacementContext.Source.Player);

	private List<SprayedInfo> m_SprayHistory = new List<SprayedInfo>(16);

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_Spray = (IngredientSpray)synchronisedObject;
		m_Adapter = new CarrierAdapter(this);
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
		float deltaTime = Time.deltaTime;
		for (int num = m_SprayHistory.Count - 1; num >= 0; num--)
		{
			SprayedInfo value = m_SprayHistory[num];
			value.m_CooldownTimer -= deltaTime;
			m_SprayHistory[num] = value;
			if (value.m_CooldownTimer <= 0f)
			{
				m_SprayHistory.RemoveAt(num);
			}
		}
		if (IsSpraying())
		{
			List<ServerIngredientContainer> allIngredientContainers = ServerIngredientContainer.GetAllIngredientContainers();
			for (int i = 0; i < allIngredientContainers.Count; i++)
			{
				GameObject gameObject = allIngredientContainers[i].gameObject;
				IHandlePlacement controllingPlacementHandler_Server = PlayerControlsHelper.GetControllingPlacementHandler_Server(gameObject);
				if (controllingPlacementHandler_Server != null && !PlacementHandlerInHistory(controllingPlacementHandler_Server) && IsInSpray(allIngredientContainers[i].transform) && controllingPlacementHandler_Server.CanHandlePlacement(m_Adapter, Vector2.up, m_PlacementContext))
				{
					controllingPlacementHandler_Server.HandlePlacement(m_Adapter, Vector2.up, m_PlacementContext);
					m_SprayHistory.Add(new SprayedInfo(controllingPlacementHandler_Server));
				}
			}
		}
		else
		{
			m_SprayHistory.Clear();
		}
	}

	private bool PlacementHandlerInHistory(IHandlePlacement iPlacement)
	{
		for (int i = 0; i < m_SprayHistory.Count; i++)
		{
			if (m_SprayHistory[i].m_IPlacement == iPlacement)
			{
				return true;
			}
		}
		return false;
	}
}
