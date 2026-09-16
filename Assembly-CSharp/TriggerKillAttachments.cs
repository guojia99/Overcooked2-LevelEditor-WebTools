using UnityEngine;

public class TriggerKillAttachments : MonoBehaviour
{
	public enum KillMode
	{
		Loose = 0,
		Attached = 1
	}

	[SerializeField]
	public string m_trigger;

	[SerializeField]
	[Mask(typeof(KillMode))]
	public int m_killMode = 2;

	private const int c_DefaultKillMode = 2;
}
