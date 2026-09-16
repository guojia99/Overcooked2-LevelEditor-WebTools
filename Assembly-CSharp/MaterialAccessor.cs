using UnityEngine;
using UnityEngine.UI;

public class MaterialAccessor : MonoBehaviour
{
	private enum MaterialAccessorType
	{
		Renderer = 0,
		Projector = 1,
		Image = 2
	}

	[SerializeField]
	private MaterialAccessorType m_accessorType;

	[SerializeField]
	[AssignComponent(Editorbility.NonEditable)]
	private Renderer m_renderer;

	[SerializeField]
	[AssignComponent(Editorbility.NonEditable)]
	private Projector m_projector;

	[SerializeField]
	[AssignComponent(Editorbility.NonEditable)]
	private Image m_image;

	public Material AccessMaterial
	{
		get
		{
			switch (m_accessorType)
			{
			case MaterialAccessorType.Renderer:
				return m_renderer.material;
			case MaterialAccessorType.Projector:
				return m_projector.material;
			case MaterialAccessorType.Image:
				return m_image.material;
			default:
				return null;
			}
		}
		set
		{
			switch (m_accessorType)
			{
			case MaterialAccessorType.Renderer:
				m_renderer.material = value;
				break;
			case MaterialAccessorType.Projector:
				m_projector.material = value;
				break;
			case MaterialAccessorType.Image:
				m_image.material = value;
				break;
			}
		}
	}
}
