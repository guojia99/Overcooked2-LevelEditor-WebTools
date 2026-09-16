using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DigitalOpus.MB.Core;
using UnityEngine;

public class MB3_TextureBaker : MB3_MeshBakerRoot
{
	public delegate void OnCombinedTexturesCoroutineSuccess();

	public delegate void OnCombinedTexturesCoroutineFail();

	public class CreateAtlasesCoroutineResult
	{
		public bool success = true;

		public bool isFinished;
	}

	public MB2_LogLevel LOG_LEVEL = MB2_LogLevel.info;

	[SerializeField]
	protected MB2_TextureBakeResults _textureBakeResults;

	[SerializeField]
	protected int _atlasPadding = 1;

	[SerializeField]
	protected int _maxAtlasSize = 4096;

	[SerializeField]
	protected bool _resizePowerOfTwoTextures;

	[SerializeField]
	protected bool _fixOutOfBoundsUVs;

	[SerializeField]
	protected int _maxTilingBakeSize = 1024;

	[SerializeField]
	protected MB2_PackingAlgorithmEnum _packingAlgorithm = MB2_PackingAlgorithmEnum.MeshBakerTexturePacker;

	[SerializeField]
	protected bool _meshBakerTexturePackerForcePowerOfTwo = true;

	[SerializeField]
	protected List<ShaderTextureProperty> _customShaderProperties = new List<ShaderTextureProperty>();

	[SerializeField]
	protected List<string> _customShaderPropNames_Depricated = new List<string>();

	[SerializeField]
	protected bool _doMultiMaterial;

	[SerializeField]
	protected bool _doMultiMaterialSplitAtlasesIfTooBig = true;

	[SerializeField]
	protected bool _doMultiMaterialSplitAtlasesIfOBUVs = true;

	[SerializeField]
	protected Material _resultMaterial;

	[SerializeField]
	protected bool _considerNonTextureProperties;

	[SerializeField]
	protected bool _doSuggestTreatment = true;

	public MB_MultiMaterial[] resultMaterials = new MB_MultiMaterial[0];

	public List<GameObject> objsToMesh;

	public OnCombinedTexturesCoroutineSuccess onBuiltAtlasesSuccess;

	public OnCombinedTexturesCoroutineFail onBuiltAtlasesFail;

	public MB_AtlasesAndRects[] OnCombinedTexturesCoroutineAtlasesAndRects;

	public override MB2_TextureBakeResults textureBakeResults
	{
		get
		{
			return _textureBakeResults;
		}
		set
		{
			_textureBakeResults = value;
		}
	}

	public virtual int atlasPadding
	{
		get
		{
			return _atlasPadding;
		}
		set
		{
			_atlasPadding = value;
		}
	}

	public virtual int maxAtlasSize
	{
		get
		{
			return _maxAtlasSize;
		}
		set
		{
			_maxAtlasSize = value;
		}
	}

	public virtual bool resizePowerOfTwoTextures
	{
		get
		{
			return _resizePowerOfTwoTextures;
		}
		set
		{
			_resizePowerOfTwoTextures = value;
		}
	}

	public virtual bool fixOutOfBoundsUVs
	{
		get
		{
			return _fixOutOfBoundsUVs;
		}
		set
		{
			_fixOutOfBoundsUVs = value;
		}
	}

	public virtual int maxTilingBakeSize
	{
		get
		{
			return _maxTilingBakeSize;
		}
		set
		{
			_maxTilingBakeSize = value;
		}
	}

	public virtual MB2_PackingAlgorithmEnum packingAlgorithm
	{
		get
		{
			return _packingAlgorithm;
		}
		set
		{
			_packingAlgorithm = value;
		}
	}

	public bool meshBakerTexturePackerForcePowerOfTwo
	{
		get
		{
			return _meshBakerTexturePackerForcePowerOfTwo;
		}
		set
		{
			_meshBakerTexturePackerForcePowerOfTwo = value;
		}
	}

	public virtual List<ShaderTextureProperty> customShaderProperties
	{
		get
		{
			return _customShaderProperties;
		}
		set
		{
			_customShaderProperties = value;
		}
	}

	public virtual List<string> customShaderPropNames
	{
		get
		{
			return _customShaderPropNames_Depricated;
		}
		set
		{
			_customShaderPropNames_Depricated = value;
		}
	}

	public virtual bool doMultiMaterial
	{
		get
		{
			return _doMultiMaterial;
		}
		set
		{
			_doMultiMaterial = value;
		}
	}

	public virtual bool doMultiMaterialSplitAtlasesIfTooBig
	{
		get
		{
			return _doMultiMaterialSplitAtlasesIfTooBig;
		}
		set
		{
			_doMultiMaterialSplitAtlasesIfTooBig = value;
		}
	}

	public virtual bool doMultiMaterialSplitAtlasesIfOBUVs
	{
		get
		{
			return _doMultiMaterialSplitAtlasesIfOBUVs;
		}
		set
		{
			_doMultiMaterialSplitAtlasesIfOBUVs = value;
		}
	}

	public virtual Material resultMaterial
	{
		get
		{
			return _resultMaterial;
		}
		set
		{
			_resultMaterial = value;
		}
	}

	public bool considerNonTextureProperties
	{
		get
		{
			return _considerNonTextureProperties;
		}
		set
		{
			_considerNonTextureProperties = value;
		}
	}

	public bool doSuggestTreatment
	{
		get
		{
			return _doSuggestTreatment;
		}
		set
		{
			_doSuggestTreatment = value;
		}
	}

	public override List<GameObject> GetObjectsToCombine()
	{
		if (objsToMesh == null)
		{
			objsToMesh = new List<GameObject>();
		}
		return objsToMesh;
	}

	public MB_AtlasesAndRects[] CreateAtlases()
	{
		return CreateAtlases(null);
	}

	public IEnumerator CreateAtlasesCoroutine(ProgressUpdateDelegate progressInfo, CreateAtlasesCoroutineResult coroutineResult, bool saveAtlasesAsAssets = false, MB2_EditorMethodsInterface editorMethods = null, float maxTimePerFrame = 0.01f)
	{
		MBVersionConcrete mbv = new MBVersionConcrete();
		if (!MB3_TextureCombiner._RunCorutineWithoutPauseIsRunning && (mbv.GetMajorVersion() < 5 || (mbv.GetMajorVersion() == 5 && mbv.GetMinorVersion() < 3)))
		{
			Debug.LogError("Running the texture combiner as a coroutine only works in Unity 5.3 and higher");
			coroutineResult.success = false;
			yield break;
		}
		OnCombinedTexturesCoroutineAtlasesAndRects = null;
		if (maxTimePerFrame <= 0f)
		{
			Debug.LogError("maxTimePerFrame must be a value greater than zero");
			coroutineResult.isFinished = true;
			yield break;
		}
		MB2_ValidationLevel vl = (Application.isPlaying ? MB2_ValidationLevel.quick : MB2_ValidationLevel.robust);
		if (!MB3_MeshBakerRoot.DoCombinedValidate(this, MB_ObjsToCombineTypes.dontCare, null, vl))
		{
			coroutineResult.isFinished = true;
			yield break;
		}
		if (_doMultiMaterial && !_ValidateResultMaterials())
		{
			coroutineResult.isFinished = true;
			yield break;
		}
		if (!_doMultiMaterial)
		{
			if (_resultMaterial == null)
			{
				Debug.LogError("Combined Material is null please create and assign a result material.");
				coroutineResult.isFinished = true;
				yield break;
			}
			Shader shader = _resultMaterial.shader;
			for (int i = 0; i < objsToMesh.Count; i++)
			{
				Material[] gOMaterials = MB_Utility.GetGOMaterials(objsToMesh[i]);
				foreach (Material material in gOMaterials)
				{
					if (material != null && material.shader != shader)
					{
						Debug.LogWarning(string.Concat("Game object ", objsToMesh[i], " does not use shader ", shader, " it may not have the required textures. If not small solid color textures will be generated."));
					}
				}
			}
		}
		MB3_TextureCombiner combiner = CreateAndConfigureTextureCombiner();
		combiner.saveAtlasesAsAssets = saveAtlasesAsAssets;
		int numResults = 1;
		if (_doMultiMaterial)
		{
			numResults = resultMaterials.Length;
		}
		OnCombinedTexturesCoroutineAtlasesAndRects = new MB_AtlasesAndRects[numResults];
		for (int k = 0; k < OnCombinedTexturesCoroutineAtlasesAndRects.Length; k++)
		{
			OnCombinedTexturesCoroutineAtlasesAndRects[k] = new MB_AtlasesAndRects();
		}
		for (int l = 0; l < OnCombinedTexturesCoroutineAtlasesAndRects.Length; l++)
		{
			Material resMatToPass = null;
			List<Material> sourceMats = null;
			if (_doMultiMaterial)
			{
				sourceMats = resultMaterials[l].sourceMaterials;
				resMatToPass = resultMaterials[l].combinedMaterial;
				combiner.fixOutOfBoundsUVs = resultMaterials[l].considerMeshUVs;
			}
			else
			{
				resMatToPass = _resultMaterial;
			}
			Debug.Log(string.Format("Creating atlases for result material {0} using shader {1}", resMatToPass, resMatToPass.shader));
			MB3_TextureCombiner.CombineTexturesIntoAtlasesCoroutineResult coroutineResult2 = new MB3_TextureCombiner.CombineTexturesIntoAtlasesCoroutineResult();
			yield return combiner.CombineTexturesIntoAtlasesCoroutine(progressInfo, OnCombinedTexturesCoroutineAtlasesAndRects[l], resMatToPass, objsToMesh, sourceMats, editorMethods, coroutineResult2, maxTimePerFrame);
			coroutineResult.success = coroutineResult2.success;
			if (!coroutineResult.success)
			{
				coroutineResult.isFinished = true;
				yield break;
			}
		}
		unpackMat2RectMap(textureBakeResults);
		textureBakeResults.doMultiMaterial = _doMultiMaterial;
		if (_doMultiMaterial)
		{
			textureBakeResults.resultMaterials = resultMaterials;
		}
		else
		{
			MB_MultiMaterial[] array = new MB_MultiMaterial[1]
			{
				new MB_MultiMaterial()
			};
			array[0].combinedMaterial = _resultMaterial;
			array[0].considerMeshUVs = _fixOutOfBoundsUVs;
			array[0].sourceMaterials = new List<Material>();
			array[0].sourceMaterials.AddRange(textureBakeResults.materials);
			textureBakeResults.resultMaterials = array;
		}
		MB3_MeshBakerCommon[] mb = GetComponentsInChildren<MB3_MeshBakerCommon>();
		for (int m = 0; m < mb.Length; m++)
		{
			mb[m].textureBakeResults = textureBakeResults;
		}
		if (LOG_LEVEL >= MB2_LogLevel.info)
		{
			Debug.Log("Created Atlases");
		}
		coroutineResult.isFinished = true;
		if (coroutineResult.success && onBuiltAtlasesSuccess != null)
		{
			onBuiltAtlasesSuccess();
		}
		if (!coroutineResult.success && onBuiltAtlasesFail != null)
		{
			onBuiltAtlasesFail();
		}
	}

	public MB_AtlasesAndRects[] CreateAtlases(ProgressUpdateDelegate progressInfo, bool saveAtlasesAsAssets = false, MB2_EditorMethodsInterface editorMethods = null)
	{
		MB_AtlasesAndRects[] array = null;
		try
		{
			CreateAtlasesCoroutineResult createAtlasesCoroutineResult = new CreateAtlasesCoroutineResult();
			MB3_TextureCombiner.RunCorutineWithoutPause(CreateAtlasesCoroutine(progressInfo, createAtlasesCoroutineResult, saveAtlasesAsAssets, editorMethods, 1000f), 0);
			if (createAtlasesCoroutineResult.success && textureBakeResults != null)
			{
				array = OnCombinedTexturesCoroutineAtlasesAndRects;
			}
		}
		catch (Exception message)
		{
			Debug.LogError(message);
		}
		finally
		{
			if (saveAtlasesAsAssets && array != null)
			{
				foreach (MB_AtlasesAndRects mB_AtlasesAndRects in array)
				{
					if (mB_AtlasesAndRects == null || mB_AtlasesAndRects.atlases == null)
					{
						continue;
					}
					for (int j = 0; j < mB_AtlasesAndRects.atlases.Length; j++)
					{
						if (mB_AtlasesAndRects.atlases[j] != null)
						{
							if (editorMethods != null)
							{
								editorMethods.Destroy(mB_AtlasesAndRects.atlases[j]);
							}
							else
							{
								MB_Utility.Destroy(mB_AtlasesAndRects.atlases[j]);
							}
						}
					}
				}
			}
		}
		return array;
	}

	private void unpackMat2RectMap(MB2_TextureBakeResults tbr)
	{
		List<Material> list = new List<Material>();
		List<MB_MaterialAndUVRect> list2 = new List<MB_MaterialAndUVRect>();
		List<Rect> list3 = new List<Rect>();
		for (int i = 0; i < OnCombinedTexturesCoroutineAtlasesAndRects.Length; i++)
		{
			MB_AtlasesAndRects mB_AtlasesAndRects = OnCombinedTexturesCoroutineAtlasesAndRects[i];
			List<MB_MaterialAndUVRect> mat2rect_map = mB_AtlasesAndRects.mat2rect_map;
			if (mat2rect_map != null)
			{
				for (int j = 0; j < mat2rect_map.Count; j++)
				{
					list2.Add(mat2rect_map[j]);
					list.Add(mat2rect_map[j].material);
					list3.Add(mat2rect_map[j].atlasRect);
				}
			}
		}
		tbr.materials = list.ToArray();
		tbr.materialsAndUVRects = list2.ToArray();
	}

	public MB3_TextureCombiner CreateAndConfigureTextureCombiner()
	{
		MB3_TextureCombiner mB3_TextureCombiner = new MB3_TextureCombiner();
		mB3_TextureCombiner.LOG_LEVEL = LOG_LEVEL;
		mB3_TextureCombiner.atlasPadding = _atlasPadding;
		mB3_TextureCombiner.maxAtlasSize = _maxAtlasSize;
		mB3_TextureCombiner.customShaderPropNames = _customShaderProperties;
		mB3_TextureCombiner.fixOutOfBoundsUVs = _fixOutOfBoundsUVs;
		mB3_TextureCombiner.maxTilingBakeSize = _maxTilingBakeSize;
		mB3_TextureCombiner.packingAlgorithm = _packingAlgorithm;
		mB3_TextureCombiner.meshBakerTexturePackerForcePowerOfTwo = _meshBakerTexturePackerForcePowerOfTwo;
		mB3_TextureCombiner.resizePowerOfTwoTextures = _resizePowerOfTwoTextures;
		mB3_TextureCombiner.considerNonTextureProperties = _considerNonTextureProperties;
		return mB3_TextureCombiner;
	}

	public static void ConfigureNewMaterialToMatchOld(Material newMat, Material original)
	{
		if (original == null)
		{
			Debug.LogWarning(string.Concat("Original material is null, could not copy properties to ", newMat, ". Setting shader to ", newMat.shader));
			return;
		}
		newMat.shader = original.shader;
		newMat.CopyPropertiesFromMaterial(original);
		ShaderTextureProperty[] shaderTexPropertyNames = MB3_TextureCombiner.shaderTexPropertyNames;
		for (int i = 0; i < shaderTexPropertyNames.Length; i++)
		{
			Vector2 one = Vector2.one;
			Vector2 zero = Vector2.zero;
			if (newMat.HasProperty(shaderTexPropertyNames[i].name))
			{
				newMat.SetTextureOffset(shaderTexPropertyNames[i].name, zero);
				newMat.SetTextureScale(shaderTexPropertyNames[i].name, one);
			}
		}
	}

	private string PrintSet(HashSet<Material> s)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (Material item in s)
		{
			stringBuilder.Append(string.Concat(item, ","));
		}
		return stringBuilder.ToString();
	}

	private bool _ValidateResultMaterials()
	{
		HashSet<Material> hashSet = new HashSet<Material>();
		for (int i = 0; i < objsToMesh.Count; i++)
		{
			if (!(objsToMesh[i] != null))
			{
				continue;
			}
			Material[] gOMaterials = MB_Utility.GetGOMaterials(objsToMesh[i]);
			for (int j = 0; j < gOMaterials.Length; j++)
			{
				if (gOMaterials[j] != null)
				{
					hashSet.Add(gOMaterials[j]);
				}
			}
		}
		HashSet<Material> hashSet2 = new HashSet<Material>();
		for (int k = 0; k < resultMaterials.Length; k++)
		{
			MB_MultiMaterial mB_MultiMaterial = resultMaterials[k];
			if (mB_MultiMaterial.combinedMaterial == null)
			{
				Debug.LogError("Combined Material is null please create and assign a result material.");
				return false;
			}
			Shader shader = mB_MultiMaterial.combinedMaterial.shader;
			for (int l = 0; l < mB_MultiMaterial.sourceMaterials.Count; l++)
			{
				if (mB_MultiMaterial.sourceMaterials[l] == null)
				{
					Debug.LogError("There are null entries in the list of Source Materials");
					return false;
				}
				if (shader != mB_MultiMaterial.sourceMaterials[l].shader)
				{
					Debug.LogWarning(string.Concat("Source material ", mB_MultiMaterial.sourceMaterials[l], " does not use shader ", shader, " it may not have the required textures. If not empty textures will be generated."));
				}
				if (hashSet2.Contains(mB_MultiMaterial.sourceMaterials[l]))
				{
					Debug.LogError(string.Concat("A Material ", mB_MultiMaterial.sourceMaterials[l], " appears more than once in the list of source materials in the source material to combined mapping. Each source material must be unique."));
					return false;
				}
				hashSet2.Add(mB_MultiMaterial.sourceMaterials[l]);
			}
		}
		if (hashSet.IsProperSubsetOf(hashSet2))
		{
			hashSet2.ExceptWith(hashSet);
			Debug.LogWarning("There are materials in the mapping that are not used on your source objects: " + PrintSet(hashSet2));
		}
		if (resultMaterials != null && resultMaterials.Length > 0 && hashSet2.IsProperSubsetOf(hashSet))
		{
			hashSet.ExceptWith(hashSet2);
			Debug.LogError("There are materials on the objects to combine that are not in the mapping: " + PrintSet(hashSet));
			return false;
		}
		return true;
	}
}
