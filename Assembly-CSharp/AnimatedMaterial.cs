using UnityEngine;

[RequireComponent(typeof(MaterialAccessor))]
[ExecuteInEditMode]
public class AnimatedMaterial : MonoBehaviour
{
	[SerializeField]
	private string m_propertyName;

	[SerializeField]
	private float m_floatValue;

	[SerializeField]
	private bool m_executeInEditMode;

	[SerializeField]
	[AssignComponent(Editorbility.NonEditable)]
	private MaterialAccessor m_materialAccessor;

	private void Awake()
	{
		OnValidate();
	}

	private void LateUpdate()
	{
		OnValidate();
	}

	private void OnValidate()
	{
		if (m_executeInEditMode || (Application.isPlaying && base.enabled))
		{
			m_materialAccessor.AccessMaterial.SetFloat(m_propertyName, m_floatValue);
		}
	}
}
