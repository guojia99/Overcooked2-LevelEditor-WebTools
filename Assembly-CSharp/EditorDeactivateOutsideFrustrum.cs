using UnityEngine;

public class EditorDeactivateOutsideFrustrum : MonoBehaviour
{
	[SerializeField]
	private Camera m_comparisonCamera;

	[SerializeField]
	private bool m_executeCull;

	[SerializeField]
	private float m_widthModifier = 1.25f;

	[SerializeField]
	private float m_heightModifier = 1.25f;

	private void Cull()
	{
		if (m_comparisonCamera == null)
		{
			return;
		}
		Renderer[] array = base.gameObject.RequestComponentsRecursive<Renderer>();
		foreach (Renderer renderer in array)
		{
			if (!InFrustrum(m_comparisonCamera, renderer.bounds))
			{
				renderer.gameObject.SetActive(false);
			}
		}
	}

	private bool InFrustrum(Camera _camera, Bounds _bounds)
	{
		return InFrustrum(_camera, _bounds.center + new Vector3(-1f, -1f, -1f).MultipliedBy(_bounds.extents)) || InFrustrum(_camera, _bounds.center + new Vector3(-1f, -1f, 1f).MultipliedBy(_bounds.extents)) || InFrustrum(_camera, _bounds.center + new Vector3(-1f, 1f, -1f).MultipliedBy(_bounds.extents)) || InFrustrum(_camera, _bounds.center + new Vector3(-1f, 1f, 1f).MultipliedBy(_bounds.extents)) || InFrustrum(_camera, _bounds.center + new Vector3(1f, -1f, -1f).MultipliedBy(_bounds.extents)) || InFrustrum(_camera, _bounds.center + new Vector3(1f, -1f, 1f).MultipliedBy(_bounds.extents)) || InFrustrum(_camera, _bounds.center + new Vector3(1f, 1f, -1f).MultipliedBy(_bounds.extents)) || InFrustrum(_camera, _bounds.center + new Vector3(1f, 1f, 1f).MultipliedBy(_bounds.extents));
	}

	private bool InFrustrum(Camera _camera, Vector3 _pos)
	{
		Vector3 vector = _camera.WorldToScreenPoint(_pos);
		float num = _camera.pixelWidth;
		float num2 = _camera.pixelHeight;
		return vector.x > (0.5f - 0.5f * m_widthModifier) * num && vector.x < (0.5f + 0.5f * m_widthModifier) * num && vector.y > (0.5f - 0.5f * m_heightModifier) * num2 && vector.y < (0.5f + 0.5f * m_heightModifier) * num2 && vector.z > _camera.nearClipPlane && vector.z < _camera.farClipPlane;
	}
}
