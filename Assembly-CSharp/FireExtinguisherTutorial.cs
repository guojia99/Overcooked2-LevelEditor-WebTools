public class FireExtinguisherTutorial : ButtonHoverIcon, ICarryNotified
{
	private bool m_hasFire;

	private bool m_beingCarried;

	protected override void Awake()
	{
		base.Awake();
		ClientFlammable.OnObjectsOnFireChanged += OnObjectsOnFireChanged;
	}

	protected override void OnDestroy()
	{
		ClientFlammable.OnObjectsOnFireChanged -= OnObjectsOnFireChanged;
		base.OnDestroy();
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

	private void OnObjectsOnFireChanged(int _count)
	{
		m_hasFire = _count > 0;
		UpdateVisiblity();
	}

	private void UpdateVisiblity()
	{
		SetVisibility(m_hasFire && !m_beingCarried && base.gameObject.activeInHierarchy);
	}
}
