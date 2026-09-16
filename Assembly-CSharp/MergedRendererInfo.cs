using UnityEngine;

[ExecuteInEditMode]
public class MergedRendererInfo : RendererInfo
{
	protected override void Start()
	{
		UpdateLighting();
	}
}
