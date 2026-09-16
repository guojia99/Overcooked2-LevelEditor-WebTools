using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine.PostProcessing
{
	public sealed class TiltShiftComponent : PostProcessingComponentCommandBuffer<TiltShiftModel>
	{
		private static class Uniforms
		{
			internal static readonly int _MainTex = Shader.PropertyToID("_MainTex");

			internal static readonly int _MainDownsampled = Shader.PropertyToID("_MainDownsampled");

			internal static readonly int _MainDownsampledHalf = Shader.PropertyToID("_MainDownsampledHalf");

			internal static readonly int _Blurred = Shader.PropertyToID("_Blurred");

			internal static readonly int _BlurStrength = Shader.PropertyToID("_BlurStrength");

			internal static readonly int _BlurArea = Shader.PropertyToID("_BlurArea");
		}

		private Mesh m_TopMesh;

		private Mesh m_BottomMesh;

		private float m_LastArea = -1f;

		private const string k_BlitShaderString = "Hidden/Post FX/Blit";

		private const string k_ShaderString = "Hidden/Post FX/Tilt Shift";

		public override bool active
		{
			get
			{
				return base.model.enabled && !context.interrupted;
			}
		}

		public override string GetName()
		{
			return "Tilt Shift";
		}

		public override CameraEvent GetCameraEvent()
		{
			return CameraEvent.BeforeImageEffects;
		}

		public override void Init(PostProcessingContext context, TiltShiftModel model)
		{
			base.Init(context, model);
			if (model.settings.BlurArea != m_LastArea)
			{
				List<Vector3> list = new List<Vector3>();
				float num = (m_LastArea = model.settings.BlurArea);
				list.Add(new Vector3(-1f, -1f));
				list.Add(new Vector3(1f, -1f + num));
				list.Add(new Vector3(-1f, -1f + num));
				list.Add(new Vector3(1f, -1f));
				List<Vector2> list2 = new List<Vector2>();
				list2.Add(new Vector2(0f, 1f));
				list2.Add(new Vector2(1f, 1f - num * 0.5f));
				list2.Add(new Vector2(0f, 1f - num * 0.5f));
				list2.Add(new Vector2(1f, 1f));
				int[] indices = new int[6] { 0, 1, 2, 0, 3, 1 };
				m_TopMesh = new Mesh();
				m_TopMesh.SetVertices(list);
				m_TopMesh.SetUVs(0, list2);
				m_TopMesh.SetIndices(indices, MeshTopology.Triangles, 0);
				list.Clear();
				list2.Clear();
				list.Add(new Vector3(-1f, 1f - num));
				list.Add(new Vector3(1f, 1f));
				list.Add(new Vector3(-1f, 1f));
				list.Add(new Vector3(1f, 1f - num));
				list2.Add(new Vector2(0f, num * 0.5f));
				list2.Add(new Vector2(1f, 0f));
				list2.Add(new Vector2(0f, 0f));
				list2.Add(new Vector2(1f, num * 0.5f));
				m_BottomMesh = new Mesh();
				m_BottomMesh.SetVertices(list);
				m_BottomMesh.SetUVs(0, list2);
				m_BottomMesh.SetIndices(indices, MeshTopology.Triangles, 0);
			}
		}

		public override void PopulateCommandBuffer(CommandBuffer cb)
		{
			TiltShiftModel.Settings settings = base.model.settings;
			Material mat = context.materialFactory.Get("Hidden/Post FX/Blit");
			Material material = context.materialFactory.Get("Hidden/Post FX/Tilt Shift");
			material.SetFloat(Uniforms._BlurArea, settings.BlurArea);
			cb.Clear();
			int width = context.width;
			int height = context.height;
			cb.SetGlobalTexture(Uniforms._MainTex, BuiltinRenderTextureType.CameraTarget);
			if (settings.Downsample)
			{
				cb.GetTemporaryRT(Uniforms._MainDownsampledHalf, width / 2, height / 2, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
				cb.GetTemporaryRT(Uniforms._MainDownsampled, width / 4, height / 4, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
				cb.GetTemporaryRT(Uniforms._Blurred, width / 4, height / 4, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
				cb.Blit(BuiltinRenderTextureType.CameraTarget, Uniforms._MainDownsampledHalf, mat, 0);
				cb.Blit(Uniforms._MainDownsampledHalf, Uniforms._MainDownsampled, mat, 0);
				cb.ReleaseTemporaryRT(Uniforms._MainDownsampledHalf);
			}
			else
			{
				cb.GetTemporaryRT(Uniforms._MainDownsampled, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
				cb.GetTemporaryRT(Uniforms._Blurred, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
				cb.Blit(BuiltinRenderTextureType.CameraTarget, Uniforms._MainDownsampled, mat, 0);
			}
			for (int i = 0; i < settings.Iterations; i++)
			{
				cb.Blit(Uniforms._MainDownsampled, Uniforms._Blurred, material, 0);
				cb.Blit(Uniforms._Blurred, Uniforms._MainDownsampled, material, 1);
			}
			cb.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
			cb.DrawMesh(m_TopMesh, Matrix4x4.identity, material, 0, 2);
			cb.DrawMesh(m_BottomMesh, Matrix4x4.identity, material, 0, 2);
			cb.ReleaseTemporaryRT(Uniforms._Blurred);
			cb.ReleaseTemporaryRT(Uniforms._MainDownsampled);
		}
	}
}
