using UnityEngine;

public static class StandardShaderHelper
{
	public enum BlendMode
	{
		Opaque = 0,
		Cutout = 1,
		Fade = 2,
		Transparent = 3
	}

	public static void SetupMaterialWithBlendMode(Material material, BlendMode blendMode)
	{
		switch (blendMode)
		{
		case BlendMode.Opaque:
			material.SetOverrideTag("RenderType", string.Empty);
			material.SetInt("_SrcBlend", 1);
			material.SetInt("_DstBlend", 0);
			material.SetInt("_ZWrite", 1);
			material.DisableKeyword("_ALPHATEST_ON");
			material.DisableKeyword("_ALPHABLEND_ON");
			material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
			material.renderQueue = -1;
			break;
		case BlendMode.Cutout:
			material.SetOverrideTag("RenderType", "TransparentCutout");
			material.SetInt("_SrcBlend", 1);
			material.SetInt("_DstBlend", 0);
			material.SetInt("_ZWrite", 1);
			material.EnableKeyword("_ALPHATEST_ON");
			material.DisableKeyword("_ALPHABLEND_ON");
			material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
			material.renderQueue = 2450;
			break;
		case BlendMode.Fade:
			material.SetOverrideTag("RenderType", "Transparent");
			material.SetInt("_SrcBlend", 5);
			material.SetInt("_DstBlend", 10);
			material.SetInt("_ZWrite", 0);
			material.DisableKeyword("_ALPHATEST_ON");
			material.EnableKeyword("_ALPHABLEND_ON");
			material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
			material.renderQueue = 3000;
			break;
		case BlendMode.Transparent:
			material.SetOverrideTag("RenderType", "Transparent");
			material.SetInt("_SrcBlend", 1);
			material.SetInt("_DstBlend", 10);
			material.SetInt("_ZWrite", 0);
			material.DisableKeyword("_ALPHATEST_ON");
			material.DisableKeyword("_ALPHABLEND_ON");
			material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
			material.renderQueue = 3000;
			break;
		}
	}
}
