using UnityEngine;

public class ServerFireHazard : ServerHazardBase
{
	private FireHazard m_hazard;

	private ServerFlammable m_flammable;

	private float m_destroyTimer;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_hazard = (FireHazard)synchronisedObject;
		m_flammable = base.gameObject.RequireComponent<ServerFlammable>();
		m_flammable.RegisterIgnitionCallback(OnIgnitionChange);
	}

	public override void UpdateSynchronising()
	{
		if (m_flammable == null)
		{
			return;
		}
		if (m_hazard.m_lifetime > 0f)
		{
			m_flammable.FightFire(m_hazard.m_lifetime, TimeManager.GetDeltaTime(base.gameObject));
		}
		if (m_destroyTimer > 0f)
		{
			m_destroyTimer -= TimeManager.GetDeltaTime(base.gameObject);
			if (m_destroyTimer <= 0f)
			{
				NetworkUtils.DestroyObject(base.gameObject);
			}
		}
	}

	public void Conflagrate()
	{
		m_flammable.Ignite();
		m_destroyTimer = 0f;
	}

	private void Shutdown()
	{
		m_destroyTimer = m_hazard.m_destroyTime;
	}

	public override void HandleTransfer(GridIndex _index, GameObject _object)
	{
		if (_object.RequestComponent<FireHazard>() == null)
		{
			m_flammable.Extinguish();
		}
	}

	private void OnIgnitionChange(bool _state)
	{
		if (!_state)
		{
			Shutdown();
		}
	}
}
