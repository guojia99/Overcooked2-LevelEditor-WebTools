using UnityEngine;

public class ServerExtinguishCollider : MonoBehaviour
{
	public void ObjectAdded(GameObject _gameObject)
	{
		ServerFlammable serverFlammable = _gameObject.RequestComponent<ServerFlammable>();
		if ((bool)serverFlammable)
		{
			serverFlammable.Extinguish();
			serverFlammable.SetCanCatchFire(false);
		}
	}

	public void ObjectRemoved(GameObject _gameObject)
	{
		ServerFlammable serverFlammable = _gameObject.RequestComponent<ServerFlammable>();
		if ((bool)serverFlammable)
		{
			serverFlammable.SetCanCatchFire(true);
		}
	}

	private void OnCollisionEnter(Collision _other)
	{
		ObjectAdded(_other.gameObject);
	}

	private void OnTriggerEnter(Collider _other)
	{
		ObjectAdded(_other.gameObject);
	}

	private void OnTriggerExit(Collider _other)
	{
		ObjectRemoved(_other.gameObject);
	}

	private void OnCollisionExit(Collision _other)
	{
		ObjectRemoved(_other.gameObject);
	}
}
