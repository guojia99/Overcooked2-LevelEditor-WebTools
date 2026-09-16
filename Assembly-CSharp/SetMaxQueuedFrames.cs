using UnityEngine;

public class SetMaxQueuedFrames : MonoBehaviour
{
	[SerializeField]
	private int m_maxQueuedFrames = 1;

	private void Awake()
	{
		QualitySettings.maxQueuedFrames = m_maxQueuedFrames;
	}
}
