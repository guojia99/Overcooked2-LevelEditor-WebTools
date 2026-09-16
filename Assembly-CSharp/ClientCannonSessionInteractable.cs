using UnityEngine;

public class ClientCannonSessionInteractable : ClientSessionInteractable
{
	private class UserSession : SessionBase
	{
		public UserSession(ClientCannonSessionInteractable _self, GameObject _avatar, ClientCannon _cannon)
			: base(_self, _avatar)
		{
		}
	}

	private ClientCannon m_cannon;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_cannon = synchronisedObject.gameObject.RequireComponent<ClientCannon>();
	}

	protected override SessionBase BuildSession(GameObject _interacter)
	{
		return new UserSession(this, _interacter, m_cannon);
	}
}
