using UnityEngine;

public class KillCollider : MonoBehaviour
{
	private void OnCollisionEnter(Collision collision)
	{
		Object.Destroy(collision.gameObject);
	}
}
