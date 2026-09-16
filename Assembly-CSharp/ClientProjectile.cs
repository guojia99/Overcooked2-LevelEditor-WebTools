using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientProjectile : ClientSynchroniserBase
{
	private Rigidbody m_Rigidbody;

	public void Start()
	{
		m_Rigidbody = base.gameObject.RequestComponentUpwardsRecursive<Rigidbody>();
		m_Rigidbody.isKinematic = true;
	}
}
