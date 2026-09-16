using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public static class PlayerControlsHelper
{
	public struct ControlAxisData
	{
		public PlayerControls.InversionType XAxisAllignment;

		public PlayerControls.InversionType YAxisAllignment;

		public ILogicalValue MoveX;

		public ILogicalValue MoveY;

		public float TurnSpeed;

		public bool QuantiseDirection;
	}

	private const int c_maxCollisionHits = 8;

	private static Collider[] s_collisions = new Collider[8];

	private static int s_fallingLayerMask = 0;

	private const float c_penetrationTestOffset = 0.0001f;

	private static int s_staticCollisionLayerMask = 0;

	private const float c_staticCollisionBoundsMultiplier = 0.5f;

	public static Vector3 GetControlAxis(PlayerControls _controls, ref ControlAxisData controlAxisData)
	{
		BuildControlAxisData(_controls, ref controlAxisData);
		return GetControlAxis(ref controlAxisData);
	}

	public static Vector3 TurnTowardsControlAxis(ref ControlAxisData controlData, PlayerControls _controls, GameObject _gameObject, float _deltaTime)
	{
		BuildControlAxisData(_controls, ref controlData);
		return TurnTowardsControlAxis(ref controlData, _gameObject, _deltaTime);
	}

	public static void BuildControlAxisData(PlayerControls _controls, ref ControlAxisData controlAxisData)
	{
		controlAxisData.XAxisAllignment = _controls.Movement.XAxisAllignment;
		controlAxisData.YAxisAllignment = _controls.Movement.YAxisAllignment;
		controlAxisData.MoveX = _controls.ControlScheme.m_moveX;
		controlAxisData.MoveY = _controls.ControlScheme.m_moveY;
		controlAxisData.TurnSpeed = _controls.Movement.TurnSpeed;
		controlAxisData.QuantiseDirection = false;
	}

	public static Vector3 GetControlAxis(ref ControlAxisData _axisData)
	{
		float num = ((_axisData.XAxisAllignment != PlayerControls.InversionType.Normal) ? (-1f) : 1f);
		float num2 = ((_axisData.YAxisAllignment != PlayerControls.InversionType.Normal) ? (-1f) : 1f);
		float x = num * _axisData.MoveX.GetValue();
		float z = num2 * (0f - _axisData.MoveY.GetValue());
		return new Vector3(x, 0f, z).normalized;
	}

	public static Vector3 GetControlAxis(float xAxis, float yAxis, PlayerControls.InversionType xAxisAllignment, PlayerControls.InversionType yAxisAllignment)
	{
		float num = ((xAxisAllignment != PlayerControls.InversionType.Normal) ? (-1f) : 1f);
		float num2 = ((yAxisAllignment != PlayerControls.InversionType.Normal) ? (-1f) : 1f);
		float x = num * xAxis;
		float z = num2 * (0f - yAxis);
		return new Vector3(x, 0f, z).normalized;
	}

	public static Vector3 TurnTowardsControlAxis(float xAxis, float yAxis, PlayerControls.InversionType xAxisAllignment, PlayerControls.InversionType yAxisAllignment, float _turnSpeed, GameObject _gameObject, float _deltaTime)
	{
		Vector3 controlAxis = GetControlAxis(xAxis, yAxis, xAxisAllignment, yAxisAllignment);
		if (controlAxis != Vector3.zero)
		{
			Vector3 forward = _gameObject.transform.forward;
			forward = Vector3.RotateTowards(forward, controlAxis, _turnSpeed * _deltaTime, 1000f).WithY(0f);
			_gameObject.transform.LookAt(_gameObject.transform.position + forward, Vector3.up);
			return controlAxis;
		}
		return Vector3.zero;
	}

	public static Vector3 TurnTowardsControlAxis(ref ControlAxisData _axisData, GameObject _gameObject, float _deltaTime)
	{
		Vector3 controlAxis = GetControlAxis(ref _axisData);
		if (controlAxis != Vector3.zero)
		{
			Vector3 forward = _gameObject.transform.forward;
			float turnSpeed = _axisData.TurnSpeed;
			forward = Vector3.RotateTowards(forward, controlAxis, turnSpeed * _deltaTime, 1000f).WithY(0f);
			_gameObject.transform.LookAt(_gameObject.transform.position + forward, Vector3.up);
			return controlAxis;
		}
		return Vector3.zero;
	}

	public static void TurnTowardsControlAxis(ref ControlAxisData _axisData, ref Vector3 _direction, float _deltaTime)
	{
		Vector3 controlAxis = GetControlAxis(ref _axisData);
		if (controlAxis != Vector3.zero)
		{
			float turnSpeed = _axisData.TurnSpeed;
			_direction = Vector3.RotateTowards(_direction, controlAxis, turnSpeed * _deltaTime, 1000f).WithY(0f);
		}
	}

	public static void TurnTowardsDirection(GameObject _gameObject, Vector3 _direction, float _turnSpeed, float _deltaTime)
	{
		if (_direction != Vector3.zero)
		{
			Vector3 forward = _gameObject.transform.forward;
			forward = Vector3.RotateTowards(forward, _direction, _turnSpeed * _deltaTime, 1000f).WithY(0f);
			_gameObject.transform.LookAt(_gameObject.transform.position + forward, Vector3.up);
		}
	}

	public static void DropHeldItem(PlayerControls _control, Vector2 _directionXZ)
	{
		ICarrier carrier = _control.gameObject.RequireInterface<ICarrier>();
		carrier.TakeItem();
	}

	public static void PlaceHeldItem_Server(PlayerControls _control, GameObject _target)
	{
		ICarrier carrier = _control.gameObject.RequireInterface<ICarrier>();
		GameObject gameObject = carrier.InspectCarriedItem();
		IHandlePlacement handlePlacement = ((!(_target != null)) ? null : GetControllingPlacementHandler_Server(_target));
		if (handlePlacement != null)
		{
			Vector2 normalized = _control.transform.forward.XZ().normalized;
			if (handlePlacement.CanHandlePlacement(carrier, normalized, new PlacementContext(PlacementContext.Source.Player)))
			{
				handlePlacement.HandlePlacement(carrier, normalized, new PlacementContext(PlacementContext.Source.Player));
			}
			else
			{
				handlePlacement.OnFailedToPlace(carrier.InspectCarriedItem());
			}
		}
		else
		{
			carrier.TakeItem();
		}
	}

	public static void PlaceHeldItem_Client(PlayerControls _control)
	{
		ICarrier carrier = _control.gameObject.RequireInterface<ICarrier>();
		GameObject gameObject = carrier.InspectCarriedItem();
		IClientHandlePlacement iHandlePlacement = _control.CurrentInteractionObjects.m_iHandlePlacement;
		if (iHandlePlacement != null)
		{
			ClientMessenger.ChefEventMessage(ChefEventMessage.ChefEventType.Place, _control.gameObject, iHandlePlacement as MonoBehaviour);
		}
		else if (gameObject != null)
		{
			ClientMessenger.ChefEventMessage(ChefEventMessage.ChefEventType.Take, _control.gameObject, iHandlePlacement as MonoBehaviour);
		}
	}

	public static IHandlePlacement GetControllingPlacementHandler_Server(GameObject _object)
	{
		ServerHandlePlacementReferral component = ComponentCache<ServerHandlePlacementReferral>.GetComponent(_object);
		if (component != null && component.enabled && component.GetHandlePlacementReferree() != null)
		{
			return component.GetHandlePlacementReferree();
		}
		IHandlePlacement[] components = ComponentCache<IHandlePlacement>.GetComponents(_object);
		return HandlePlacementUtils.GetHighestPriority(components);
	}

	public static IClientHandlePlacement GetControllingPlacementHandler_Client(GameObject _object)
	{
		ClientHandlePlacementReferral component = ComponentCache<ClientHandlePlacementReferral>.GetComponent(_object);
		if (component != null && component.enabled && component.GetHandlePlacementReferree() != null)
		{
			return component.GetHandlePlacementReferree();
		}
		IClientHandlePlacement[] components = ComponentCache<IClientHandlePlacement>.GetComponents(_object);
		IClientHandlePlacement clientHandlePlacement = null;
		foreach (IClientHandlePlacement clientHandlePlacement2 in components)
		{
			if ((clientHandlePlacement2 as MonoBehaviour).enabled && (clientHandlePlacement == null || clientHandlePlacement2.GetPlacementPriority() > clientHandlePlacement.GetPlacementPriority()))
			{
				clientHandlePlacement = clientHandlePlacement2;
			}
		}
		return clientHandlePlacement;
	}

	public static IClientHandlePickup GetControllingPickupHandler_Client(GameObject _object)
	{
		ClientHandlePickupReferral component = ComponentCache<ClientHandlePickupReferral>.GetComponent(_object);
		if (component != null && component.enabled && component.GetHandlePickupReferree() != null)
		{
			return component.GetHandlePickupReferree();
		}
		IClientHandlePickup[] components = ComponentCache<IClientHandlePickup>.GetComponents(_object);
		IClientHandlePickup clientHandlePickup = null;
		foreach (IClientHandlePickup clientHandlePickup2 in components)
		{
			if ((clientHandlePickup2 as MonoBehaviour).enabled && (clientHandlePickup == null || clientHandlePickup2.GetPickupPriority() > clientHandlePickup.GetPickupPriority()))
			{
				clientHandlePickup = clientHandlePickup2;
			}
		}
		return clientHandlePickup;
	}

	public static IHandlePickup GetControllingPickupHandler_Server(GameObject _object)
	{
		ServerHandlePickupReferral serverHandlePickupReferral = _object.RequestComponent<ServerHandlePickupReferral>();
		if (serverHandlePickupReferral != null && serverHandlePickupReferral.enabled && serverHandlePickupReferral.GetHandlePickupReferree() != null)
		{
			return serverHandlePickupReferral.GetHandlePickupReferree();
		}
		IHandlePickup[] array = _object.RequestInterfaces<IHandlePickup>();
		IHandlePickup handlePickup = null;
		foreach (IHandlePickup handlePickup2 in array)
		{
			if ((handlePickup2 as MonoBehaviour).enabled && (handlePickup == null || handlePickup2.GetPickupPriority() > handlePickup.GetPickupPriority()))
			{
				handlePickup = handlePickup2;
			}
		}
		return handlePickup;
	}

	public static bool WouldFallIfHoldingNothing(PlayerControls _control)
	{
		if (_control.GroundCollider != null)
		{
			return false;
		}
		if (s_fallingLayerMask == 0)
		{
			s_fallingLayerMask = LayerMask.GetMask("Default", "Ground", "Walls", "Worktops");
		}
		ICarrier carrier = _control.gameObject.RequireInterface<ICarrier>();
		IAttachment attachment = carrier.InspectCarriedItem().RequestInterface<IAttachment>();
		Collider collider = attachment.AccessGameObject().RequestComponent<Collider>();
		Bounds bounds = collider.bounds;
		Quaternion rotation = collider.transform.rotation;
		Vector3 position = collider.transform.position;
		int num = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, s_collisions, rotation, s_fallingLayerMask, QueryTriggerInteraction.Ignore);
		for (int i = 0; i < num; i++)
		{
			Collider collider2 = s_collisions[i];
			Transform transform = collider2.transform;
			Vector3 direction = Vector3.zero;
			float distance = 0f;
			Vector3 vector = (transform.position - position).normalized * 0.0001f;
			if (Physics.ComputePenetration(collider, position + vector, rotation, collider2, transform.position, transform.rotation, out direction, out distance))
			{
				float num2 = Mathf.Clamp(Vector3.Dot(direction, Vector3.up), -1f, 1f);
				if (num2 > 0.5f)
				{
					return true;
				}
			}
		}
		return false;
	}

	public static bool IsHeldItemInsideStaticCollision(PlayerControls _control)
	{
		if (s_staticCollisionLayerMask == 0)
		{
			s_staticCollisionLayerMask = LayerMask.GetMask("Default", "Ground", "Walls", "Worktops", "PlateStationBlock", "CookingStationBlock");
		}
		ICarrier carrier = _control.gameObject.RequireInterface<ICarrier>();
		IAttachment attachment = carrier.InspectCarriedItem().RequestInterface<IAttachment>();
		Collider collider = attachment.AccessGameObject().RequestComponent<Collider>();
		Bounds bounds = collider.bounds;
		Quaternion rotation = collider.transform.rotation;
		Vector3 position = collider.transform.position;
		Vector3 halfExtents = bounds.extents * 0.5f;
		int num = Physics.OverlapBoxNonAlloc(bounds.center, halfExtents, s_collisions, rotation, s_staticCollisionLayerMask, QueryTriggerInteraction.Ignore);
		if (num > 0)
		{
			return true;
		}
		return false;
	}
}
