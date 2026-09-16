using UnityEngine;

public class MapLoaderManager : Manager
{
	[SerializeField]
	public static MapLoaderManager s_instance;

	private MultiplayerController m_multiplayerController;

	private bool m_bStarted;

	protected virtual void Awake()
	{
		s_instance = this;
		m_multiplayerController = GameUtils.RequestManagerInterface<MultiplayerController>();
	}

	private void Update()
	{
		if (!m_bStarted && ConnectionModeSwitcher.GetStatus().GetProgress() == eConnectionModeSwitchProgress.Complete)
		{
			m_multiplayerController.StartWorldMap();
			m_bStarted = true;
		}
	}

	private void OnDestroy()
	{
		m_multiplayerController.StopWorldMap();
	}
}
