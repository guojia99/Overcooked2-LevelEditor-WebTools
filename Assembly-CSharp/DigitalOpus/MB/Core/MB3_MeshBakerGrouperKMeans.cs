using System;
using System.Collections.Generic;
using UnityEngine;

namespace DigitalOpus.MB.Core
{
	[Serializable]
	public class MB3_MeshBakerGrouperKMeans : MB3_MeshBakerGrouperCore
	{
		public int numClusters = 4;

		public Vector3[] clusterCenters = new Vector3[0];

		public float[] clusterSizes = new float[0];

		public MB3_MeshBakerGrouperKMeans(GrouperData data)
		{
			d = data;
		}

		public override Dictionary<string, List<Renderer>> FilterIntoGroups(List<GameObject> selection)
		{
			Dictionary<string, List<Renderer>> dictionary = new Dictionary<string, List<Renderer>>();
			List<GameObject> list = new List<GameObject>();
			int num = 20;
			foreach (GameObject item in selection)
			{
				if (!(item == null))
				{
					GameObject gameObject = item;
					Renderer component = gameObject.GetComponent<Renderer>();
					if (component is MeshRenderer || component is SkinnedMeshRenderer)
					{
						list.Add(gameObject);
					}
				}
			}
			if (list.Count > 0 && num > 0 && num < list.Count)
			{
				MB3_KMeansClustering mB3_KMeansClustering = new MB3_KMeansClustering(list, num);
				mB3_KMeansClustering.Cluster();
				clusterCenters = new Vector3[num];
				clusterSizes = new float[num];
				for (int i = 0; i < num; i++)
				{
					List<Renderer> cluster = mB3_KMeansClustering.GetCluster(i, out clusterCenters[i], out clusterSizes[i]);
					if (cluster.Count > 0)
					{
						dictionary.Add("Cluster_" + i, cluster);
					}
				}
			}
			return dictionary;
		}

		public override void DrawGizmos(Bounds sceneObjectBounds)
		{
			if (clusterCenters != null && clusterSizes != null && clusterCenters.Length == clusterSizes.Length)
			{
				for (int i = 0; i < clusterSizes.Length; i++)
				{
					Gizmos.DrawWireSphere(clusterCenters[i], clusterSizes[i]);
				}
			}
		}
	}
}
