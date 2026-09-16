using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
public class UIColorOverride : MonoBehaviour
{
	[SerializeField]
	private Color m_color = Color.white;

	private void LateUpdate()
	{
		Image[] array = base.gameObject.RequestComponentsRecursive<Image>();
		foreach (Image image in array)
		{
			image.color = m_color;
		}
	}
}
