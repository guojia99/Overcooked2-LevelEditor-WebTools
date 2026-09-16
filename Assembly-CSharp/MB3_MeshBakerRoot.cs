using System.Collections.Generic;
using DigitalOpus.MB.Core;
using UnityEngine;

public abstract class MB3_MeshBakerRoot : MonoBehaviour
{
	public class ZSortObjects
	{
		public class Item
		{
			public GameObject go;

			public Vector3 point;
		}

		public class ItemComparer : IComparer<Item>
		{
			public int Compare(Item a, Item b)
			{
				return (int)Mathf.Sign(b.point.z - a.point.z);
			}
		}

		public Vector3 sortAxis;

		public void SortByDistanceAlongAxis(List<GameObject> gos)
		{
			if (sortAxis == Vector3.zero)
			{
				Debug.LogError("The sort axis cannot be the zero vector.");
				return;
			}
			Debug.Log("Z sorting meshes along axis numObjs=" + gos.Count);
			List<Item> list = new List<Item>();
			Quaternion quaternion = Quaternion.FromToRotation(sortAxis, Vector3.forward);
			for (int i = 0; i < gos.Count; i++)
			{
				if (gos[i] != null)
				{
					Item item = new Item();
					item.point = gos[i].transform.position;
					item.go = gos[i];
					item.point = quaternion * item.point;
					list.Add(item);
				}
			}
			list.Sort(new ItemComparer());
			for (int j = 0; j < gos.Count; j++)
			{
				gos[j] = list[j].go;
			}
		}
	}

	public static bool DO_INTEGRITY_CHECKS;

	public Vector3 sortAxis;

	[HideInInspector]
	public abstract MB2_TextureBakeResults textureBakeResults { get; set; }

	public virtual List<GameObject> GetObjectsToCombine()
	{
		return null;
	}

	public static bool DoCombinedValidate(MB3_MeshBakerRoot mom, MB_ObjsToCombineTypes objToCombineType, MB2_EditorMethodsInterface editorMethods, MB2_ValidationLevel validationLevel)
	{
		if (mom.textureBakeResults == null)
		{
			Debug.LogError("Need to set Texture Bake Result on " + mom);
			return false;
		}
		if (mom is MB3_MeshBakerCommon)
		{
			MB3_MeshBakerCommon mB3_MeshBakerCommon = (MB3_MeshBakerCommon)mom;
			MB3_TextureBaker textureBaker = mB3_MeshBakerCommon.GetTextureBaker();
			if (textureBaker != null && textureBaker.textureBakeResults != mom.textureBakeResults)
			{
				Debug.LogWarning("Texture Bake Result on this component is not the same as the Texture Bake Result on the MB3_TextureBaker.");
			}
		}
		Dictionary<int, MB_Utility.MeshAnalysisResult> dictionary = null;
		if (validationLevel == MB2_ValidationLevel.robust)
		{
			dictionary = new Dictionary<int, MB_Utility.MeshAnalysisResult>();
		}
		List<GameObject> objectsToCombine = mom.GetObjectsToCombine();
		for (int i = 0; i < objectsToCombine.Count; i++)
		{
			GameObject gameObject = objectsToCombine[i];
			if (gameObject == null)
			{
				Debug.LogError("The list of objects to combine contains a null at position." + i + " Select and use [shift] delete to remove");
				return false;
			}
			for (int j = i + 1; j < objectsToCombine.Count; j++)
			{
				if (objectsToCombine[i] == objectsToCombine[j])
				{
					Debug.LogError("The list of objects to combine contains duplicates at " + i + " and " + j);
					return false;
				}
			}
			if (MB_Utility.GetGOMaterials(gameObject).Length == 0)
			{
				Debug.LogError(string.Concat("Object ", gameObject, " in the list of objects to be combined does not have a material"));
				return false;
			}
			Mesh mesh = MB_Utility.GetMesh(gameObject);
			if (mesh == null)
			{
				Debug.LogError(string.Concat("Object ", gameObject, " in the list of objects to be combined does not have a mesh"));
				return false;
			}
			if (mesh != null && !Application.isEditor && Application.isPlaying && mom.textureBakeResults.doMultiMaterial && validationLevel >= MB2_ValidationLevel.robust)
			{
				MB_Utility.MeshAnalysisResult value;
				if (!dictionary.TryGetValue(mesh.GetInstanceID(), out value))
				{
					MB_Utility.doSubmeshesShareVertsOrTris(mesh, ref value);
					dictionary.Add(mesh.GetInstanceID(), value);
				}
				if (value.hasOverlappingSubmeshVerts)
				{
					Debug.LogWarning(string.Concat("Object ", objectsToCombine[i], " in the list of objects to combine has overlapping submeshes (submeshes share vertices). If the UVs associated with the shared vertices are important then this bake may not work. If you are using multiple materials then this object can only be combined with objects that use the exact same set of textures (each atlas contains one texture). There may be other undesirable side affects as well. Mesh Master, available in the asset store can fix overlapping submeshes."));
				}
			}
		}
		List<GameObject> list = objectsToCombine;
		if (mom is MB3_MeshBaker)
		{
			list = mom.GetObjectsToCombine();
			if (list == null || list.Count == 0)
			{
				Debug.LogError("No meshes to combine. Please assign some meshes to combine.");
				return false;
			}
			if (mom is MB3_MeshBaker && ((MB3_MeshBaker)mom).meshCombiner.renderType == MB_RenderType.skinnedMeshRenderer && !editorMethods.ValidateSkinnedMeshes(list))
			{
				return false;
			}
		}
		if (editorMethods != null)
		{
			editorMethods.CheckPrefabTypes(objToCombineType, objectsToCombine);
		}
		return true;
	}
}
