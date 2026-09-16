using UnityEngine;

public class InstanceMaterialScroller : MonoBehaviour
{
	[SerializeField]
	private int m_materialIndex;

	[SerializeField]
	private Vector2 m_scrollSpeed;

	private MeshRenderer m_meshRenderer;

	private Material m_material;

	private void Awake()
	{
	}
}
