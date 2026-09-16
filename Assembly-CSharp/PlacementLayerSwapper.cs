using UnityEngine;

public class PlacementLayerSwapper : MonoBehaviour, ICarryNotified
{
	[SerializeField]
	public string m_layerWhenPlaced;

	private int m_defaultLayerId;

	private int m_layerWhenPlacedId;

	private bool m_carried;

	private bool m_onSurface;

	private void Awake()
	{
		m_defaultLayerId = LayerMask.NameToLayer("Attachments");
		m_layerWhenPlacedId = LayerMask.NameToLayer("HeldAttachments");
		base.gameObject.layer = m_defaultLayerId;
	}

	private void UpdateLayer()
	{
		if (m_carried)
		{
			base.gameObject.layer = m_layerWhenPlacedId;
		}
		else
		{
			base.gameObject.layer = m_defaultLayerId;
		}
	}

	public void OnCarryBegun(ICarrier _carrier)
	{
		m_carried = true;
		UpdateLayer();
	}

	public void OnCarryEnded(ICarrier _carrier)
	{
		m_carried = false;
		UpdateLayer();
	}
}
