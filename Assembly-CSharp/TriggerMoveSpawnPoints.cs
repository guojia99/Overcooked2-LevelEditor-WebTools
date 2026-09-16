using UnityEngine;

public class TriggerMoveSpawnPoints : MonoBehaviour
{
	[SerializeField]
	public string m_trigger = string.Empty;

	[SerializeField]
	public bool m_movePlayersImmediately;

	[SerializeField]
	public Transform[] m_spawnPoints = new Transform[0];
}
