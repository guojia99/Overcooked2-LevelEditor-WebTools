using UnityEngine;

public class MaterialScroller : MonoBehaviour
{
	[SerializeField]
	private Material m_scrollingMaterial;

	[SerializeField]
	private Vector2 m_scrollSpeed;

	private void Update()
	{
		m_scrollingMaterial.mainTextureOffset += TimeManager.GetDeltaTime(base.gameObject) * m_scrollSpeed;
	}
}
