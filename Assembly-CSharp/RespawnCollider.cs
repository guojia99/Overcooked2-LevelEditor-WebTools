using UnityEngine;

public class RespawnCollider : MonoBehaviour
{
	public enum RespawnType
	{
		Hit = 0,
		Drowning = 1,
		FallDeath = 2,
		Car = 3
	}

	[SerializeField]
	public RespawnType m_respawnType = RespawnType.FallDeath;

	[SerializeField]
	public LayerMask m_respawnFilter = -1;

	[SerializeField]
	public bool m_onlyRespawnables;

	[SerializeField]
	public string m_onRespawnTrigger = string.Empty;

	[SerializeField]
	public GameObject m_onDeathEffect;
}
