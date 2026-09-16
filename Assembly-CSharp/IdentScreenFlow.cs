using InControl;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class IdentScreenFlow : MonoBehaviour
{
	[SerializeField]
	[SceneName]
	private string m_nextSceneName = "StartScreen";

	private Animator m_animator;

	private ILogicalButton m_skipButton;

	private AsyncOperation m_async;

	private OptionsData optionsData;

	private SceneLoaderHelper m_SceneLoaderHelper;

	public ProgressBarUI m_ProgressBar;

	private static readonly int m_iSkip = Animator.StringToHash("Skip");

	private bool m_bSetOptions;

	private void Start()
	{
		optionsData = new OptionsData();
		optionsData.OnAwake();
		optionsData.LoadFromSave();
		optionsData = null;
		m_skipButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UISelect);
		m_animator = base.gameObject.RequestComponent<Animator>();
		m_SceneLoaderHelper = base.gameObject.AddComponent<SceneLoaderHelper>();
		m_SceneLoaderHelper.LoadLevelAsync(m_nextSceneName, false);
	}

	private void Update()
	{
		if (!m_bSetOptions && GameUtils.GetGameMetaEnvironment() != null)
		{
			AudioManager audioManager = GameUtils.RequestManager<AudioManager>();
			if (audioManager != null)
			{
				optionsData = new OptionsData();
				optionsData.OnAwake();
				optionsData.LoadFromSave();
				optionsData = null;
				m_bSetOptions = true;
			}
		}
		if (m_skipButton.JustPressed() || InputManager.ActiveDevice.Action1.IsPressed)
		{
			m_animator.SetTrigger(m_iSkip);
		}
		if (m_ProgressBar != null)
		{
			if (m_SceneLoaderHelper != null)
			{
				m_ProgressBar.SetValue(m_SceneLoaderHelper.GetProgress());
			}
			else
			{
				m_ProgressBar.SetValue(0f);
			}
		}
	}

	public void ActivateNextScene()
	{
		m_SceneLoaderHelper.ActivateSceneWhenLoaded = true;
	}
}
