using UnityEngine;

public class RendererInfo : MonoBehaviour
{
	public int lightmapIndex = -1;

	public Vector4 lightmapScaleOffset = default(Vector4);

	protected virtual void Start()
	{
		UpdateLighting();
	}

	protected void UpdateLighting()
	{
		MeshRenderer component = GetComponent<MeshRenderer>();
		if (component != null)
		{
			component.lightmapIndex = lightmapIndex;
			component.lightmapScaleOffset = lightmapScaleOffset;
		}
	}
}
