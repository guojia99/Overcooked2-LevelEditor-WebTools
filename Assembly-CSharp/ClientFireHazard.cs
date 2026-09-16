using UnityEngine;

public class ClientFireHazard : ClientHazardBase
{
	private FireHazard m_hazard;

	private ClientFlammable m_flammable;

	private Collider m_collider;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_hazard = (FireHazard)synchronisedObject;
		m_flammable = base.gameObject.RequireComponent<ClientFlammable>();
		m_flammable.RegisterIgnitionCallback(OnIgnitionChange);
	}

	protected virtual void Awake()
	{
		m_collider = base.gameObject.RequireComponent<Collider>();
		m_collider.enabled = false;
	}

	private void SetHazardActive(bool _active)
	{
		if (m_collider != null)
		{
			m_collider.enabled = _active;
		}
	}

	private void OnIgnitionChange(bool _state)
	{
		SetHazardActive(_state);
	}
}
