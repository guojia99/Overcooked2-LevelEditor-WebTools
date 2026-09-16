using UnityEngine;

public class PlayerControlsImpl_OnFire : MonoBehaviour, IPlayerControlsImpl
{
	private PlayerControls m_controls;

	private ICarrier m_iCarrier;

	private PlayerControlsHelper.ControlAxisData m_controlAxisData = default(PlayerControlsHelper.ControlAxisData);

	private void Awake()
	{
		base.enabled = false;
	}

	public void Init(PlayerControls _controls)
	{
		m_controls = _controls;
	}

	public void Enable()
	{
		if (m_iCarrier.InspectCarriedItem() != null)
		{
			Vector2 normalized = base.gameObject.transform.forward.XZ().normalized;
			PlayerControlsHelper.PlaceHeldItem_Client(m_controls);
		}
		base.enabled = true;
	}

	public void Update_Impl()
	{
		float deltaTime = TimeManager.GetDeltaTime(base.gameObject);
		Update_Movement(deltaTime);
	}

	public void Disable()
	{
		base.enabled = false;
	}

	private void Update_Movement(float _deltaTime)
	{
		PlayerControlsHelper.TurnTowardsControlAxis(ref m_controlAxisData, m_controls, base.gameObject, _deltaTime);
		float runSpeed = m_controls.Movement.RunSpeed;
		base.gameObject.GetComponent<Rigidbody>().velocity = m_controls.MovementScale * base.gameObject.transform.forward * runSpeed;
	}
}
