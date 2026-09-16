using UnityEngine;

public class ItemCarrierAdapter : ICarrierPlacement
{
	private GameObject m_item;

	public ItemCarrierAdapter(GameObject _item)
	{
		m_item = _item;
	}

	public GameObject InspectCarriedItem()
	{
		return m_item;
	}

	public void DestroyCarriedItem()
	{
		m_item = null;
	}

	public GameObject TakeItem()
	{
		GameObject item = m_item;
		m_item = null;
		return item;
	}
}
