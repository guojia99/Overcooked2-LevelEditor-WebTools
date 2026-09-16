using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerCatchableItem : ServerSynchroniserBase, ICatchable
{
	private const float c_minFlightTime = 0.1f;

	private IThrowable m_throwable;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_throwable = base.gameObject.GetComponent<IThrowable>();
	}

	public bool AllowCatch(IHandleCatch _catcher, Vector2 _directionXZ)
	{
		if (m_throwable == null)
		{
			return false;
		}
		GameObject gameObject = (_catcher as MonoBehaviour).gameObject;
		if (_catcher != null && gameObject == null)
		{
			return false;
		}
		if (!(gameObject.RequestComponent<ServerAttachStation>() != null) && !(gameObject.RequestComponent<ServerIngredientContainer>() != null) && (!m_throwable.IsFlying() || (m_throwable.IsFlying() && m_throwable.GetFlightTime() < 0.1f)))
		{
			return false;
		}
		return true;
	}

	public GameObject AccessGameObject()
	{
		return base.gameObject;
	}
}
