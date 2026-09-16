using UnityEngine;
using UnityEngine.Rendering;

public class DisableReflectionProbes : MonoBehaviour
{
	private void Start()
	{
		bool supports3DRenderTextures = SystemInfo.supports3DRenderTextures;
		bool supportsCubemapArrayTextures = SystemInfo.supportsCubemapArrayTextures;
		bool supportsRenderToCubemap = SystemInfo.supportsRenderToCubemap;
		if (supports3DRenderTextures && supportsCubemapArrayTextures && supportsRenderToCubemap)
		{
			return;
		}
		MeshRenderer[] array = base.gameObject.RequestComponentsInImmediateChildren<MeshRenderer>();
		if (array != null)
		{
			for (int i = 0; i < array.Length; i++)
			{
				array[i].reflectionProbeUsage = ReflectionProbeUsage.Off;
			}
		}
	}
}
