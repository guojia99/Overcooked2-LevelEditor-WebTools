using UnityEngine;

public class ServerPlayerRespawnManager : MonoBehaviour
{
	private static ServerPlayerRespawnManager ms_Instance;

	public void StartRespawning(IRespawnBehaviour _iRespawnBehaviour, ServerRespawnCollider _collider)
	{
		StartCoroutine(_iRespawnBehaviour.RespawnCoroutine(_collider));
	}

	private void Awake()
	{
		ms_Instance = this;
	}

	private void OnDestroy()
	{
		ms_Instance = null;
	}

	public static void KillOrRespawn(GameObject _gameObject, ServerRespawnCollider _collider)
	{
		IRespawnBehaviour respawnBehaviour = _gameObject.RequestInterfaceRecursive<IRespawnBehaviour>();
		if (respawnBehaviour != null)
		{
			if (ms_Instance != null)
			{
				ms_Instance.StartRespawning(respawnBehaviour, _collider);
			}
		}
		else
		{
			NetworkUtils.DestroyObject(_gameObject);
		}
	}
}
