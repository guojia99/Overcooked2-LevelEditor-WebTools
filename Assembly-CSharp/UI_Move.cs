using UnityEngine;
using UnityEngine.UI;

public class UI_Move : MonoBehaviour
{
	[SerializeField]
	private Vector2 m_Offset = Vector2.zero;

	[SerializeField]
	[AssignResource("UI_Move", Editorbility.Editable)]
	public Material m_UIMaterial;

	private Graphic[] m_GraphicsToMove;

	private Material m_Material;

	private static readonly int m_OffsetUniform = Shader.PropertyToID("_Offset");

	public Vector2 Offset
	{
		get
		{
			return m_Offset;
		}
		set
		{
			m_Offset = value;
			if (m_Material != null)
			{
				m_Material.SetVector(m_OffsetUniform, m_Offset);
			}
		}
	}

	private void Start()
	{
		if (m_UIMaterial == null)
		{
		}
		if (m_Material == null && m_UIMaterial != null)
		{
			m_Material = new Material(m_UIMaterial);
		}
		UpdateGraphics();
	}

	public void UpdateGraphics()
	{
		m_GraphicsToMove = null;
		if (!(m_UIMaterial == null))
		{
			if (m_Material == null)
			{
				m_Material = new Material(m_UIMaterial);
			}
			m_GraphicsToMove = base.gameObject.RequestComponentsRecursive<Graphic>();
			int num = m_GraphicsToMove.Length;
			for (int i = 0; i < num; i++)
			{
				m_GraphicsToMove[i].material = m_Material;
			}
		}
	}

	private void LateUpdate()
	{
		if (m_GraphicsToMove == null || m_GraphicsToMove.Length == 0)
		{
			UpdateGraphics();
		}
		m_Material.SetVector(m_OffsetUniform, m_Offset);
	}
}
