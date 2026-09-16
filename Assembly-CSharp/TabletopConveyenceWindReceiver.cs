using UnityEngine;

public class TabletopConveyenceWindReceiver : TabletopWindReceiver
{
	private ServerConveyorStation m_conveyor;

	private IConveyenceReceiver m_receiver;

	protected override void Start()
	{
		m_receiver = base.gameObject.RequestInterface<IConveyenceReceiver>();
		m_conveyor = base.gameObject.RequestComponent<ServerConveyorStation>();
		if (m_conveyor != null)
		{
			m_conveyor.RegisterAllowConveyCallback(AllowConveying);
		}
	}

	protected override bool ShouldDetach(GameObject _object)
	{
		if (m_conveyor != null && m_conveyor.IsConveying())
		{
			return false;
		}
		if (m_receiver != null && m_receiver.IsReceiving())
		{
			return false;
		}
		return base.ShouldDetach(_object);
	}

	private bool AllowConveying()
	{
		return !IsTooMuchWind(GetVelocity());
	}
}
