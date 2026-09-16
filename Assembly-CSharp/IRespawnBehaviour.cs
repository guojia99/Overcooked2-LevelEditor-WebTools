using System.Collections;

public interface IRespawnBehaviour
{
	IEnumerator RespawnCoroutine(ServerRespawnCollider _collider);
}
