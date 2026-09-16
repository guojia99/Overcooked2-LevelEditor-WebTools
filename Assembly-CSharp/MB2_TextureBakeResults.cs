using System.Collections.Generic;
using System.Text;
using DigitalOpus.MB.Core;
using UnityEngine;

public class MB2_TextureBakeResults : ScriptableObject
{
	public class Material2AtlasRectangleMapper
	{
		private MB2_TextureBakeResults tbr;

		private int[] numTimesMatAppearsInAtlas;

		private MB_MaterialAndUVRect[] matsAndSrcUVRect;

		public Material2AtlasRectangleMapper(MB2_TextureBakeResults res)
		{
			tbr = res;
			matsAndSrcUVRect = res.materialsAndUVRects;
			numTimesMatAppearsInAtlas = new int[matsAndSrcUVRect.Length];
			for (int i = 0; i < matsAndSrcUVRect.Length; i++)
			{
				if (numTimesMatAppearsInAtlas[i] > 1)
				{
					continue;
				}
				int num = 1;
				for (int j = i + 1; j < matsAndSrcUVRect.Length; j++)
				{
					if (matsAndSrcUVRect[i].material == matsAndSrcUVRect[j].material)
					{
						num++;
					}
				}
				numTimesMatAppearsInAtlas[i] = num;
				if (num <= 1)
				{
					continue;
				}
				for (int k = i + 1; k < matsAndSrcUVRect.Length; k++)
				{
					if (matsAndSrcUVRect[i].material == matsAndSrcUVRect[k].material)
					{
						numTimesMatAppearsInAtlas[k] = num;
					}
				}
			}
		}

		public bool TryMapMaterialToUVRect(Material mat, Mesh m, int submeshIdx, int idxInResultMats, MB3_MeshCombinerSingle.MeshChannelsCache meshChannelCache, Dictionary<int, MB_Utility.MeshAnalysisResult[]> meshAnalysisCache, out Rect rectInAtlas, out Rect encapsulatingRect, out Rect sourceMaterialTilingOut, ref string errorMsg, MB2_LogLevel logLevel)
		{
			if (tbr.materialsAndUVRects.Length == 0 && tbr.materials.Length > 0)
			{
				errorMsg = "The 'Texture Bake Result' needs to be re-baked to be compatible with this version of Mesh Baker. Please re-bake using the MB3_TextureBaker.";
				rectInAtlas = default(Rect);
				encapsulatingRect = default(Rect);
				sourceMaterialTilingOut = default(Rect);
				return false;
			}
			if (mat == null)
			{
				rectInAtlas = default(Rect);
				encapsulatingRect = default(Rect);
				sourceMaterialTilingOut = default(Rect);
				errorMsg = string.Format("Mesh {0} Had no material on submesh {1} cannot map to a material in the atlas", m.name, submeshIdx);
				return false;
			}
			if (submeshIdx >= m.subMeshCount)
			{
				errorMsg = "Submesh index is greater than the number of submeshes";
				rectInAtlas = default(Rect);
				encapsulatingRect = default(Rect);
				sourceMaterialTilingOut = default(Rect);
				return false;
			}
			int num = -1;
			for (int i = 0; i < matsAndSrcUVRect.Length; i++)
			{
				if (mat == matsAndSrcUVRect[i].material)
				{
					num = i;
					break;
				}
			}
			if (num == -1)
			{
				rectInAtlas = default(Rect);
				encapsulatingRect = default(Rect);
				sourceMaterialTilingOut = default(Rect);
				errorMsg = string.Format("Material {0} could not be found in the Texture Bake Result", mat.name);
				return false;
			}
			if (!tbr.resultMaterials[idxInResultMats].considerMeshUVs)
			{
				if (numTimesMatAppearsInAtlas[num] != 1)
				{
					Debug.LogError("There is a problem with this TextureBakeResults. FixOutOfBoundsUVs is false and a material appears more than once.");
				}
				rectInAtlas = matsAndSrcUVRect[num].atlasRect;
				encapsulatingRect = matsAndSrcUVRect[num].samplingEncapsulatinRect;
				sourceMaterialTilingOut = matsAndSrcUVRect[num].sourceMaterialTiling;
				return true;
			}
			MB_Utility.MeshAnalysisResult[] value;
			if (!meshAnalysisCache.TryGetValue(m.GetInstanceID(), out value))
			{
				value = new MB_Utility.MeshAnalysisResult[m.subMeshCount];
				for (int j = 0; j < m.subMeshCount; j++)
				{
					Vector2[] uv0Raw = meshChannelCache.GetUv0Raw(m);
					MB_Utility.hasOutOfBoundsUVs(uv0Raw, m, ref value[j], j);
				}
				meshAnalysisCache.Add(m.GetInstanceID(), value);
			}
			bool flag = false;
			if (logLevel >= MB2_LogLevel.trace)
			{
				Debug.LogWarning(string.Format("Trying to find a rectangle in atlas capable of holding tiled sampling rect for mesh {0} using material {1}", m, mat));
			}
			for (int k = num; k < matsAndSrcUVRect.Length; k++)
			{
				if (!(matsAndSrcUVRect[k].material == mat))
				{
					continue;
				}
				Rect rect = default(Rect);
				Rect r = value[submeshIdx].uvRect;
				Rect r2 = matsAndSrcUVRect[k].sourceMaterialTiling;
				Rect r3 = matsAndSrcUVRect[k].samplingEncapsulatinRect;
				MB3_UVTransformUtility.Canonicalize(ref r3, 0f, 0f);
				rect = MB3_UVTransformUtility.CombineTransforms(ref r, ref r2);
				if (logLevel >= MB2_LogLevel.trace)
				{
					Debug.Log("uvR=" + r.ToString("f5") + " matR=" + r2.ToString("f5") + "Potential Rect " + rect.ToString("f5") + " encapsulating=" + r3.ToString("f5"));
				}
				MB3_UVTransformUtility.Canonicalize(ref rect, r3.x, r3.y);
				if (logLevel >= MB2_LogLevel.trace)
				{
					Debug.Log("Potential Rect Cannonical " + rect.ToString("f5") + " encapsulating=" + r3.ToString("f5"));
				}
				if (MB3_UVTransformUtility.RectContains(ref r3, ref rect))
				{
					if (logLevel >= MB2_LogLevel.trace)
					{
						Debug.Log(string.Concat("Found rect in atlas capable of containing tiled sampling rect for mesh ", m, " at idx=", k));
					}
					num = k;
					flag = true;
					break;
				}
			}
			if (flag)
			{
				rectInAtlas = matsAndSrcUVRect[num].atlasRect;
				encapsulatingRect = matsAndSrcUVRect[num].samplingEncapsulatinRect;
				sourceMaterialTilingOut = matsAndSrcUVRect[num].sourceMaterialTiling;
				return true;
			}
			rectInAtlas = default(Rect);
			encapsulatingRect = default(Rect);
			sourceMaterialTilingOut = default(Rect);
			errorMsg = string.Format("Could not find a tiled rectangle in the atlas capable of containing the uv and material tiling on mesh {0} for material {1}", m.name, mat);
			return false;
		}
	}

	private const int VERSION = 3230;

	public int version;

	public MB_MaterialAndUVRect[] materialsAndUVRects;

	public MB_MultiMaterial[] resultMaterials;

	public bool doMultiMaterial;

	public Material[] materials;

	public bool fixOutOfBoundsUVs;

	public Material resultMaterial;

	private void OnEnable()
	{
		if (version < 3230 && resultMaterials != null)
		{
			for (int i = 0; i < resultMaterials.Length; i++)
			{
				resultMaterials[i].considerMeshUVs = fixOutOfBoundsUVs;
			}
		}
		version = 3230;
	}

	public static MB2_TextureBakeResults CreateForMaterialsOnRenderer(GameObject[] gos, List<Material> matsOnTargetRenderer)
	{
		HashSet<Material> hashSet = new HashSet<Material>(matsOnTargetRenderer);
		for (int i = 0; i < gos.Length; i++)
		{
			if (gos[i] == null)
			{
				Debug.LogError(string.Format("Game object {0} in list of objects to add was null", i));
				return null;
			}
			Material[] gOMaterials = MB_Utility.GetGOMaterials(gos[i]);
			if (gOMaterials.Length == 0)
			{
				Debug.LogError(string.Format("Game object {0} in list of objects to add no renderer", i));
				return null;
			}
			for (int j = 0; j < gOMaterials.Length; j++)
			{
				if (!hashSet.Contains(gOMaterials[j]))
				{
					hashSet.Add(gOMaterials[j]);
				}
			}
		}
		Material[] array = new Material[hashSet.Count];
		hashSet.CopyTo(array);
		MB2_TextureBakeResults mB2_TextureBakeResults = (MB2_TextureBakeResults)ScriptableObject.CreateInstance(typeof(MB2_TextureBakeResults));
		List<MB_MaterialAndUVRect> list = new List<MB_MaterialAndUVRect>();
		for (int k = 0; k < array.Length; k++)
		{
			if (array[k] != null)
			{
				MB_MaterialAndUVRect item = new MB_MaterialAndUVRect(array[k], new Rect(0f, 0f, 1f, 1f), new Rect(0f, 0f, 1f, 1f), new Rect(0f, 0f, 1f, 1f), new Rect(0f, 0f, 1f, 1f), string.Empty);
				if (!list.Contains(item))
				{
					list.Add(item);
				}
			}
		}
		Material[] array2 = (mB2_TextureBakeResults.materials = new Material[list.Count]);
		mB2_TextureBakeResults.resultMaterials = new MB_MultiMaterial[list.Count];
		for (int l = 0; l < list.Count; l++)
		{
			array2[l] = list[l].material;
			mB2_TextureBakeResults.resultMaterials[l] = new MB_MultiMaterial();
			List<Material> list2 = new List<Material>();
			list2.Add(list[l].material);
			mB2_TextureBakeResults.resultMaterials[l].sourceMaterials = list2;
			mB2_TextureBakeResults.resultMaterials[l].combinedMaterial = array2[l];
			mB2_TextureBakeResults.resultMaterials[l].considerMeshUVs = false;
		}
		if (array.Length == 1)
		{
			mB2_TextureBakeResults.doMultiMaterial = false;
		}
		else
		{
			mB2_TextureBakeResults.doMultiMaterial = true;
		}
		mB2_TextureBakeResults.materialsAndUVRects = list.ToArray();
		return mB2_TextureBakeResults;
	}

	public bool DoAnyResultMatsUseConsiderMeshUVs()
	{
		if (resultMaterials == null)
		{
			return false;
		}
		for (int i = 0; i < resultMaterials.Length; i++)
		{
			if (resultMaterials[i].considerMeshUVs)
			{
				return true;
			}
		}
		return false;
	}

	public bool ContainsMaterial(Material m)
	{
		for (int i = 0; i < materialsAndUVRects.Length; i++)
		{
			if (materialsAndUVRects[i].material == m)
			{
				return true;
			}
		}
		return false;
	}

	public string GetDescription()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("Shaders:\n");
		HashSet<Shader> hashSet = new HashSet<Shader>();
		if (materialsAndUVRects != null)
		{
			for (int i = 0; i < materialsAndUVRects.Length; i++)
			{
				if (materialsAndUVRects[i].material != null)
				{
					hashSet.Add(materialsAndUVRects[i].material.shader);
				}
			}
		}
		foreach (Shader item in hashSet)
		{
			stringBuilder.Append("  ").Append(item.name).AppendLine();
		}
		stringBuilder.Append("Materials:\n");
		if (materialsAndUVRects != null)
		{
			for (int j = 0; j < materialsAndUVRects.Length; j++)
			{
				if (materialsAndUVRects[j].material != null)
				{
					stringBuilder.Append("  ").Append(materialsAndUVRects[j].material.name).AppendLine();
				}
			}
		}
		return stringBuilder.ToString();
	}
}
