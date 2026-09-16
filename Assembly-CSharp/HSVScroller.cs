using UnityEngine;

public class HSVScroller : MonoBehaviour
{
	[SerializeField]
	private Vector3 m_scrollSpeed;

	private Material m_material;

	private Vector3 m_hsvValue;

	private void Update()
	{
		if (m_material == null)
		{
			Renderer renderer = base.gameObject.RequireComponent<Renderer>();
			m_material = renderer.material;
		}
		else
		{
			Color.RGBToHSV(m_material.color, out m_hsvValue.x, out m_hsvValue.y, out m_hsvValue.z);
			m_hsvValue += m_scrollSpeed * TimeManager.GetDeltaTime(base.gameObject);
			m_material.color = Color.HSVToRGB(m_hsvValue.x, m_hsvValue.y, m_hsvValue.z);
		}
	}
}
