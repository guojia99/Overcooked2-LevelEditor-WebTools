using UnityEngine;
using UnityEngine.UI;

public class AwardAvatarUIController : MonoBehaviour
{
	[SerializeField]
	private Image m_button;

	[SerializeField]
	public float m_awardTimeout = 5f;

	[SerializeField]
	private Text m_name;

	[SerializeField]
	private ChefColourData m_colour;

	[SerializeField]
	private ChefAvatarData m_avatar;

	[SerializeField]
	private Color m_AmbientColor;

	public T17Text m_WaitingForPlayersText;

	private FrontendChef m_chef;

	[SerializeField]
	private Animator m_animator;

	[SerializeField]
	private GameOneShotAudioTag m_audioTag = GameOneShotAudioTag.Blank;

	private static readonly int m_iUnlock = Animator.StringToHash("Unlock");

	private void Awake()
	{
		m_animator = base.gameObject.RequireComponent<Animator>();
		m_chef = base.gameObject.RequireComponentRecursive<FrontendChef>();
		if (m_button != null)
		{
			m_button.enabled = false;
		}
	}

	public void EnableButton()
	{
		if (m_button != null)
		{
			m_button.enabled = true;
		}
	}

	public void SetData(ChefAvatarData _chef)
	{
		GameSession.SelectedChefData chefData = new GameSession.SelectedChefData(_chef, m_colour);
		m_chef.SetShaderMode(FrontendChef.ShaderMode.eUI);
		m_chef.SetChefData(chefData);
		m_chef.SetUIChefAmbientLighting(m_AmbientColor);
		m_name.text = string.Empty;
		if ((bool)m_animator)
		{
			m_animator.SetTrigger(m_iUnlock);
		}
		GameUtils.TriggerAudio(m_audioTag, base.gameObject.layer);
	}
}
