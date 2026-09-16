using UnityEngine;

public class TabletopWindReceiver : WindAccumulator
{
	[SerializeField]
	private float m_minDetachForce;

	private ServerAttachStation m_attachStation;

	protected override void Start()
	{
		m_attachStation = base.gameObject.RequestComponent<ServerAttachStation>();
	}

	protected override void Update()
	{
		base.Update();
		if (m_attachStation != null)
		{
			Collider collider = m_attachStation.gameObject.RequireComponent<Collider>();
			collider.enabled = !IsTooMuchWind(GetVelocity());
			GameObject gameObject = m_attachStation.InspectItem();
			if (gameObject != null && ShouldDetach(gameObject))
			{
				Detach();
			}
		}
	}

	protected void Detach()
	{
		m_attachStation.TakeItem();
	}

	protected virtual bool ShouldDetach(GameObject _object)
	{
		return IsTooMuchWind(GetVelocity()) && _object.RequestInterface<IWindReceiver>() != null;
	}

	protected bool IsTooMuchWind(Vector3 _velocity)
	{
		float num = m_minDetachForce * m_minDetachForce;
		return _velocity.sqrMagnitude > num;
	}
}
