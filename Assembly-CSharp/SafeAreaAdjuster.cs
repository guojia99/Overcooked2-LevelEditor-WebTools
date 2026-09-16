using UnityEngine;

public class SafeAreaAdjuster : MonoBehaviour
{
	public static float SafeAreaWidth = 1f;

	public static float SafeAreaHeight = 1f;

	private bool m_bCompleted;

	[SerializeField]
	private float m_kerchunkRate = 0.12f;

	private RectTransform m_uiRect;

	private MetaGameProgress m_metaGame;

	private IOption m_widthOption;

	private IOption m_heightOption;

	private ILogicalValue m_leftRightAxis;

	private ILogicalValue m_upDownAxis;

	private ILogicalButton m_confirmButton;

	private Vector2 m_scrollAccumulator;

	private bool m_buttonReleased;

	public bool Completed
	{
		get
		{
			return m_bCompleted;
		}
	}

	public static bool CanBeAdjusted()
	{
		return true;
	}

	public void Show()
	{
		m_buttonReleased = false;
		m_bCompleted = false;
		base.enabled = true;
		m_uiRect = base.gameObject.RequireComponent<RectTransform>();
		UpdateSafeAreaUISize();
		m_leftRightAxis = PlayerInputLookup.GetUIValue(PlayerInputLookup.LogicalValueID.MovementX);
		m_upDownAxis = PlayerInputLookup.GetUIValue(PlayerInputLookup.LogicalValueID.MovementY);
		m_confirmButton = PlayerInputLookup.GetUIButton(PlayerInputLookup.LogicalButtonID.UISelect);
		m_metaGame = GameUtils.GetMetaGameProgress();
		m_widthOption = m_metaGame.GetOption(OptionsData.OptionType.SafeAreaX);
		m_heightOption = m_metaGame.GetOption(OptionsData.OptionType.SafeAreaY);
	}

	public void Hide()
	{
		m_bCompleted = true;
		base.enabled = false;
		if (!(m_metaGame != null))
		{
			return;
		}
		IOption option = m_metaGame.GetOption(OptionsData.OptionType.HasSetSafeArea);
		option.SetOption(1);
		if (CanBeAdjusted())
		{
			SaveManager saveManager = GameUtils.RequireManager<SaveManager>();
			saveManager.RegisterOnIdle(delegate
			{
				saveManager.SaveMetaProgress();
			});
		}
	}

	protected void Update()
	{
		float deltaTime = TimeManager.GetDeltaTime(base.gameObject);
		AdvanceAxis(m_widthOption, m_heightOption, m_leftRightAxis, m_upDownAxis, deltaTime, ref m_scrollAccumulator);
		UpdateSafeAreaUISize();
		if (m_buttonReleased && m_confirmButton.JustReleased())
		{
			Hide();
		}
		if (!m_buttonReleased && !m_confirmButton.IsDown())
		{
			m_buttonReleased = true;
		}
	}

	private void UpdateSafeAreaUISize()
	{
		float num = (1f - SafeAreaWidth) * 0.5f;
		float num2 = (1f - SafeAreaHeight) * 0.5f;
		m_uiRect.anchorMin = new Vector2(num, num2);
		m_uiRect.anchorMax = new Vector2(1f - num, 1f - num2);
	}

	private void AdvanceAxis(IOption _optionWidth, IOption _optionHeight, ILogicalValue _iLValueX, ILogicalValue _iLValueY, float _dt, ref Vector2 _cumulator)
	{
		float value = _iLValueX.GetValue();
		float value2 = _iLValueY.GetValue();
		int kerchunkedMove = GetKerchunkedMove((0f - value) * _dt, ref _cumulator.x);
		_optionWidth.SetOption(_optionWidth.GetOption() + kerchunkedMove);
		int kerchunkedMove2 = GetKerchunkedMove((0f - value2) * _dt, ref _cumulator.y);
		_optionHeight.SetOption(_optionHeight.GetOption() + kerchunkedMove2);
	}

	private int GetKerchunkedMove(float _analogDiff, ref float _cumulator)
	{
		if (_cumulator * _analogDiff <= 0f)
		{
			_cumulator = _analogDiff;
		}
		else
		{
			_cumulator += _analogDiff;
		}
		float quotient;
		MathUtils.TruncModf(_cumulator, m_kerchunkRate, out quotient, out _cumulator);
		return (int)quotient;
	}
}
