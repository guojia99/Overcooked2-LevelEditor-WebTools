using UnityEngine;
using UnityEngine.Audio;

public class AudioSourceMixerAssignment : MonoBehaviour
{
	[SerializeField]
	private string m_groupName;

	[SerializeField]
	[AssignComponent(Editorbility.NonEditable)]
	private AudioSource m_audioSource;

	private void Awake()
	{
		AudioManager audioManager = GameUtils.RequestManager<AudioManager>();
		AudioMixer audioMixer = audioManager.m_audioMixer;
		m_audioSource.outputAudioMixerGroup = audioMixer.FindMatchingGroups(m_groupName)[0];
	}
}
