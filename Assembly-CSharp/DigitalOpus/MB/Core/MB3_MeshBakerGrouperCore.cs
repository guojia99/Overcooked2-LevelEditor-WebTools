using System;
using System.Collections.Generic;
using UnityEngine;

namespace DigitalOpus.MB.Core
{
	[Serializable]
	public abstract class MB3_MeshBakerGrouperCore
	{
		public GrouperData d;

		public abstract Dictionary<string, List<Renderer>> FilterIntoGroups(List<GameObject> selection);

		public abstract void DrawGizmos(Bounds sourceObjectBounds);

		public void DoClustering(MB3_TextureBaker tb, MB3_MeshBakerGrouper grouper)
		{
			Dictionary<string, List<Renderer>> dictionary = FilterIntoGroups(tb.GetObjectsToCombine());
			if (d.clusterOnLMIndex)
			{
				Dictionary<string, List<Renderer>> dictionary2 = new Dictionary<string, List<Renderer>>();
				foreach (string key4 in dictionary.Keys)
				{
					List<Renderer> gaws = dictionary[key4];
					Dictionary<int, List<Renderer>> dictionary3 = GroupByLightmapIndex(gaws);
					foreach (int key5 in dictionary3.Keys)
					{
						string key = key4 + "-LM-" + key5;
						dictionary2.Add(key, dictionary3[key5]);
					}
				}
				dictionary = dictionary2;
			}
			if (d.clusterByLODLevel)
			{
				Dictionary<string, List<Renderer>> dictionary4 = new Dictionary<string, List<Renderer>>();
				foreach (string key6 in dictionary.Keys)
				{
					List<Renderer> list = dictionary[key6];
					foreach (Renderer r in list)
					{
						if (r == null)
						{
							continue;
						}
						bool flag = false;
						LODGroup componentInParent = r.GetComponentInParent<LODGroup>();
						if (componentInParent != null)
						{
							LOD[] lODs = componentInParent.GetLODs();
							for (int i = 0; i < lODs.Length; i++)
							{
								LOD lOD = lODs[i];
								if (Array.Find(lOD.renderers, (Renderer x) => x == r) != null)
								{
									flag = true;
									string key2 = string.Format("{0}_LOD{1}", key6, i);
									List<Renderer> value;
									if (!dictionary4.TryGetValue(key2, out value))
									{
										value = new List<Renderer>();
										dictionary4.Add(key2, value);
									}
									if (!value.Contains(r))
									{
										value.Add(r);
									}
								}
							}
						}
						if (!flag)
						{
							string key3 = string.Format("{0}_LOD0", key6);
							List<Renderer> value2;
							if (!dictionary4.TryGetValue(key3, out value2))
							{
								value2 = new List<Renderer>();
								dictionary4.Add(key3, value2);
							}
							if (!value2.Contains(r))
							{
								value2.Add(r);
							}
						}
					}
				}
				dictionary = dictionary4;
			}
			int num = 0;
			foreach (string key7 in dictionary.Keys)
			{
				List<Renderer> list2 = dictionary[key7];
				if (list2.Count > 1 || grouper.data.includeCellsWithOnlyOneRenderer)
				{
					AddMeshBaker(tb, key7, list2);
				}
				else
				{
					num++;
				}
			}
			Debug.Log(string.Format("Found {0} cells with Renderers. Not creating bakers for {1} because there is only one mesh in the cell. Creating {2} bakers.", dictionary.Count, num, dictionary.Count - num));
		}

		private Dictionary<int, List<Renderer>> GroupByLightmapIndex(List<Renderer> gaws)
		{
			Dictionary<int, List<Renderer>> dictionary = new Dictionary<int, List<Renderer>>();
			for (int i = 0; i < gaws.Count; i++)
			{
				List<Renderer> list = null;
				if (dictionary.ContainsKey(gaws[i].lightmapIndex))
				{
					list = dictionary[gaws[i].lightmapIndex];
				}
				else
				{
					list = new List<Renderer>();
					dictionary.Add(gaws[i].lightmapIndex, list);
				}
				list.Add(gaws[i]);
			}
			return dictionary;
		}

		private void AddMeshBaker(MB3_TextureBaker tb, string key, List<Renderer> gaws)
		{
			int num = 0;
			for (int i = 0; i < gaws.Count; i++)
			{
				Mesh mesh = MB_Utility.GetMesh(gaws[i].gameObject);
				if (mesh != null)
				{
					num += mesh.vertexCount;
				}
			}
			GameObject gameObject = new GameObject("MeshBaker-" + key);
			gameObject.transform.position = Vector3.zero;
			MB3_MeshBakerCommon mB3_MeshBakerCommon;
			if (num >= 65535)
			{
				mB3_MeshBakerCommon = gameObject.AddComponent<MB3_MultiMeshBaker>();
				mB3_MeshBakerCommon.useObjsToMeshFromTexBaker = false;
			}
			else
			{
				mB3_MeshBakerCommon = gameObject.AddComponent<MB3_MeshBaker>();
				mB3_MeshBakerCommon.useObjsToMeshFromTexBaker = false;
			}
			mB3_MeshBakerCommon.textureBakeResults = tb.textureBakeResults;
			mB3_MeshBakerCommon.transform.parent = tb.transform;
			for (int j = 0; j < gaws.Count; j++)
			{
				mB3_MeshBakerCommon.GetObjectsToCombine().Add(gaws[j].gameObject);
			}
		}
	}
}
