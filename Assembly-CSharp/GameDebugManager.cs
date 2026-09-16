using UnityEngine;

public class GameDebugManager : Manager
{
	[SerializeField]
	private GameDebugConfig m_config;

	private float m_manualTimeMod = 1f;

	private void Start()
	{
		AudioSource component = Camera.main.GetComponent<AudioSource>();
		if (component != null && DebugManager.Instance.GetOption("Mute music"))
		{
			component.enabled = false;
		}
		OnScreenDebugDisplay onScreenDebugDisplay = base.gameObject.AddComponent<OnScreenDebugDisplay>();
		if (onScreenDebugDisplay != null)
		{
			onScreenDebugDisplay.AddDisplay(new VersionDisplay());
		}
	}

	private void Update()
	{
		if (Time.timeScale != 0f)
		{
			Time.timeScale = m_manualTimeMod * m_config.m_timeScale;
			Time.fixedDeltaTime = m_manualTimeMod * m_config.m_timeScale * 0.02f;
		}
	}

	public GameDebugConfig GetDebugConfig()
	{
		return m_config;
	}
}
