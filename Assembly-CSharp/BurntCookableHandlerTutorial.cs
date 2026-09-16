using UnityEngine;

public class BurntCookableHandlerTutorial : ButtonHoverIcon, ICarryNotified
{
	[SerializeField]
	[Mask(typeof(CookingStationType))]
	private int m_typeMask;

	private int m_burntCookables;

	private bool m_beingCarried;

	protected override void Awake()
	{
		base.Awake();
		ClientCookingHandler.OnBurntCookableAdded += OnBurntCookableAdded;
		ClientCookingHandler.OnBurntCookableRemoved += OnBurntCookableRemoved;
	}

	public void OnCarryBegun(ICarrier _carrier)
	{
		m_beingCarried = true;
		UpdateVisiblity();
	}

	public void OnCarryEnded(ICarrier _carrier)
	{
		m_beingCarried = false;
		UpdateVisiblity();
	}

	private void OnBurntCookableAdded(ClientCookingHandler _cookable)
	{
		if (MaskUtils.HasFlag(m_typeMask, _cookable.GetRequiredStationType()))
		{
			m_burntCookables++;
			UpdateVisiblity();
		}
	}

	private void OnBurntCookableRemoved(ClientCookingHandler _cookable)
	{
		if (MaskUtils.HasFlag(m_typeMask, _cookable.GetRequiredStationType()))
		{
			m_burntCookables--;
			UpdateVisiblity();
		}
	}

	private void UpdateVisiblity()
	{
		SetVisibility(m_burntCookables > 0 && !m_beingCarried);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		ClientCookingHandler.OnBurntCookableAdded -= OnBurntCookableAdded;
		ClientCookingHandler.OnBurntCookableRemoved -= OnBurntCookableRemoved;
	}
}
