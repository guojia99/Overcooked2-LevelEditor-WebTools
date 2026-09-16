using UnityEngine.Rendering;

namespace UnityEngine.PostProcessing
{
	public sealed class AmbientOcclusionComponent : PostProcessingComponentCommandBuffer<AmbientOcclusionModel>
	{
		private static class Uniforms
		{
			internal static readonly int _MainTex = Shader.PropertyToID("_MainTex");

			internal static readonly int _AOTex = Shader.PropertyToID("_AOTex");

			internal static readonly int _Intensity = Shader.PropertyToID("_Intensity");

			internal static readonly int _Radius = Shader.PropertyToID("_Radius");

			internal static readonly int _RotationTex = Shader.PropertyToID("_RotationTex");

			internal static readonly int _TempBlur = Shader.PropertyToID("_TempBlur");

			internal static readonly int _BlurSize = Shader.PropertyToID("_BlurSize");

			internal static readonly int _Downsample = Shader.PropertyToID("_Downsample");

			internal static readonly int _DownsampledDepth = Shader.PropertyToID("_DownsampledDepth");

			internal static readonly int _DistanceFalloff = Shader.PropertyToID("_DistanceFalloff");
		}

		private const string k_BlitShaderString = "Hidden/Post FX/Blit";

		private const string k_ShaderString = "Hidden/Post FX/Ambient Occlusion";

		public override bool active
		{
			get
			{
				return base.model.enabled && base.model.settings.intensity > 0f && !context.interrupted;
			}
		}

		public override DepthTextureMode GetCameraFlags()
		{
			return DepthTextureMode.Depth;
		}

		public override string GetName()
		{
			return "Ambient Occlusion";
		}

		public override CameraEvent GetCameraEvent()
		{
			return CameraEvent.BeforeImageEffectsOpaque;
		}

		public override void PopulateCommandBuffer(CommandBuffer cb)
		{
			AmbientOcclusionModel.Settings settings = base.model.settings;
			Material material = context.materialFactory.Get("Hidden/Post FX/Ambient Occlusion");
			material.SetFloat(Uniforms._Intensity, settings.intensity);
			material.SetFloat(Uniforms._Radius, settings.radius);
			material.SetTexture(Uniforms._RotationTex, settings.RotationTexture);
			material.SetFloat(Uniforms._BlurSize, settings.blurSize);
			material.SetFloat(Uniforms._Downsample, (!settings.downsampling) ? 1f : 2f);
			material.SetFloat(Uniforms._DistanceFalloff, settings.distanceFalloff);
			material.shaderKeywords = null;
			switch (settings.sampleCount)
			{
			case AmbientOcclusionModel.SampleCount.Lowest:
				material.EnableKeyword("FOUR_SAMPLES");
				break;
			case AmbientOcclusionModel.SampleCount.Low:
				material.EnableKeyword("FIVE_SAMPLES");
				break;
			case AmbientOcclusionModel.SampleCount.Medium:
				material.EnableKeyword("TEN_SAMPLES");
				break;
			case AmbientOcclusionModel.SampleCount.High:
				material.EnableKeyword("TWELVE_SAMPLES");
				break;
			}
			int num = context.width;
			int num2 = context.height;
			if (settings.downsampling)
			{
				num /= 2;
				num2 /= 2;
			}
			cb.Clear();
			cb.GetTemporaryRT(Uniforms._DownsampledDepth, num, num2, 0, FilterMode.Point, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
			cb.Blit(null, Uniforms._DownsampledDepth, material, 4);
			cb.GetTemporaryRT(Uniforms._AOTex, num, num2, 0, FilterMode.Bilinear, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
			cb.Blit(null, Uniforms._AOTex, material, 0);
			cb.GetTemporaryRT(Uniforms._TempBlur, num, num2, 0, FilterMode.Bilinear, RenderTextureFormat.R8);
			cb.Blit(null, Uniforms._TempBlur, material, 1);
			cb.Blit(null, Uniforms._AOTex, material, 2);
			cb.Blit(null, BuiltinRenderTextureType.CameraTarget, material, 3);
			cb.ReleaseTemporaryRT(Uniforms._AOTex);
			cb.ReleaseTemporaryRT(Uniforms._TempBlur);
		}
	}
}
