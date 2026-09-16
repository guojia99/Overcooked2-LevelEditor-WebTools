using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
[RequireComponent(typeof(Image))]
public class CircleScreenWipeExposer : MonoBehaviour
{
	[SerializeField]
	[Range(0f, 1f)]
	private float m_prop;

	[SerializeField]
	private bool m_executeInEditMode;

	public float Prop
	{
		get
		{
			return m_prop;
		}
		set
		{
			m_prop = value;
		}
	}

	private float ImageProp
	{
		get
		{
			Image image = base.gameObject.RequireComponent<Image>();
			return image.material.GetFloat("_Prop");
		}
		set
		{
			Image image = base.gameObject.RequireComponent<Image>();
			image.material.SetFloat("_Prop", value);
			image.SetMaterialDirty();
			Mask mask = base.gameObject.RequestComponent<Mask>();
			if (mask != null)
			{
				mask.enabled = false;
				mask.enabled = true;
			}
		}
	}

	private void Awake()
	{
		OnValidate();
	}

	private void Update()
	{
		OnValidate();
	}

	private void OnValidate()
	{
		if (m_executeInEditMode || Application.isPlaying)
		{
			ImageProp = m_prop;
		}
		else if (!Application.isPlaying && !m_executeInEditMode && ImageProp != 1f)
		{
			ImageProp = 1f;
		}
	}
}
