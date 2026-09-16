using UnityEngine;

public class GameEnvironmentSingleton : MonoBehaviour
{
	private static GameObject s_gameEnvironment;

	public static GameObject GetActiveGameObject()
	{
		return s_gameEnvironment;
	}

	private void Awake()
	{
		s_gameEnvironment = base.gameObject;
	}

	private void OnDestroy()
	{
		s_gameEnvironment = null;
	}
}
