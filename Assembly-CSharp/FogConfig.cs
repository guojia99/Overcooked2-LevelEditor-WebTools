using UnityEngine;

public class FogConfig : MonoBehaviour
{
	public enum Kind
	{
		Distance = 0,
		Height = 1
	}

	[SerializeField]
	public Kind m_fogKind;

	[SerializeField]
	public float m_fogOffset;

	[SerializeField]
	public float m_fogNear;

	[SerializeField]
	public float m_fogFar = 100f;

	[SerializeField]
	public Color m_fogColour = Color.grey;

	private void OnEnable()
	{
		ForceUpdate();
	}

	public void ForceUpdate()
	{
		switch (m_fogKind)
		{
		case Kind.Distance:
			Shader.SetGlobalFloat("_Overcooked2FogNear", m_fogNear);
			Shader.SetGlobalFloat("_Overcooked2FogFar", m_fogFar);
			Shader.SetGlobalColor("_Overcooked2FogColour", m_fogColour);
			break;
		case Kind.Height:
			Shader.SetGlobalFloat("_Overcooked2FogOffset", 0f - m_fogOffset);
			Shader.SetGlobalFloat("_Overcooked2FogNear", m_fogNear);
			Shader.SetGlobalFloat("_Overcooked2FogFar", m_fogFar);
			Shader.SetGlobalColor("_Overcooked2FogColour", m_fogColour);
			break;
		}
	}

	private void OnDisable()
	{
		Shader.SetGlobalFloat("_Overcooked2FogOffset", 0f);
		Shader.SetGlobalFloat("_Overcooked2FogNear", 0f);
		Shader.SetGlobalFloat("_Overcooked2FogFar", 0f);
		Shader.SetGlobalColor("_Overcooked2FogColour", Color.black);
	}
}
