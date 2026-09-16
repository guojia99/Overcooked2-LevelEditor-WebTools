using UnityEngine;

[AddComponentMenu("Scripts/Core/Components/TriggerAudio")]
public class TriggerAudio : MonoBehaviour
{
	[SerializeField]
	private GameOneShotAudioTag m_oneShotTag;

	[SerializeField]
	private string m_trigger;

	[SerializeField]
	private bool m_onEnable;

	[SerializeField]
	private bool m_onDisable;

	private void Awake()
	{
	}

	private void OnEnable()
	{
		if (m_onEnable)
		{
			GameUtils.TriggerAudio(m_oneShotTag, base.gameObject.layer);
		}
	}

	private void OnDisable()
	{
		if (m_onDisable)
		{
			GameUtils.TriggerAudio(m_oneShotTag, base.gameObject.layer);
		}
	}

	private void OnTrigger(string _name)
	{
		if (_name == m_trigger)
		{
			GameUtils.TriggerAudio(m_oneShotTag, base.gameObject.layer);
		}
	}
}
