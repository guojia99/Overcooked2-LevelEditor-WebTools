using UnityEngine;

public class Cannon : MonoBehaviour
{
	[SerializeField]
	public ProjectileAnimation m_animation;

	[SerializeField]
	public string m_launchTrigger;

	[SerializeField]
	public string m_enableTrigger;

	[SerializeField]
	public string m_disableTrigger;

	[SerializeField]
	public GameObject m_button;

	public GenericVoid<GameObject> EndCannonRoutine = delegate
	{
	};

	[HideInInspector]
	public Transform m_target;

	[HideInInspector]
	public Transform m_attachPoint;

	[HideInInspector]
	public Transform m_exitPoint;

	private void Awake()
	{
		m_target = base.transform.FindChildRecursive("Target");
		m_attachPoint = base.transform.FindChildRecursive("AttachPoint");
		m_exitPoint = base.transform.FindChildRecursive("ExitPoint");
	}
}
