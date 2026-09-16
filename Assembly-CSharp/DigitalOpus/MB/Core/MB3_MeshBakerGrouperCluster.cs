using System;
using System.Collections.Generic;
using UnityEngine;

namespace DigitalOpus.MB.Core
{
	[Serializable]
	public class MB3_MeshBakerGrouperCluster : MB3_MeshBakerGrouperCore
	{
		public MB3_AgglomerativeClustering cluster;

		private float _lastMaxDistBetweenClusters;

		public float _ObjsExtents = 10f;

		public float _minDistBetweenClusters = 0.001f;

		private List<MB3_AgglomerativeClustering.ClusterNode> _clustersToDraw = new List<MB3_AgglomerativeClustering.ClusterNode>();

		private float[] _radii;

		public MB3_MeshBakerGrouperCluster(GrouperData data, List<GameObject> gos)
		{
			d = data;
		}

		public override Dictionary<string, List<Renderer>> FilterIntoGroups(List<GameObject> selection)
		{
			Dictionary<string, List<Renderer>> dictionary = new Dictionary<string, List<Renderer>>();
			for (int i = 0; i < _clustersToDraw.Count; i++)
			{
				MB3_AgglomerativeClustering.ClusterNode clusterNode = _clustersToDraw[i];
				List<Renderer> list = new List<Renderer>();
				for (int j = 0; j < clusterNode.leafs.Length; j++)
				{
					Renderer component = cluster.clusters[clusterNode.leafs[j]].leaf.go.GetComponent<Renderer>();
					if (component is MeshRenderer || component is SkinnedMeshRenderer)
					{
						list.Add(component);
					}
				}
				dictionary.Add("Cluster_" + i, list);
			}
			return dictionary;
		}

		public void BuildClusters(List<GameObject> gos, ProgressUpdateCancelableDelegate progFunc)
		{
			if (gos.Count == 0)
			{
				Debug.LogWarning("No objects to cluster. Add some objects to the list of Objects To Combine.");
				return;
			}
			if (cluster == null)
			{
				cluster = new MB3_AgglomerativeClustering();
			}
			List<MB3_AgglomerativeClustering.item_s> list = new List<MB3_AgglomerativeClustering.item_s>();
			for (int i = 0; i < gos.Count; i++)
			{
				if (gos[i] != null && list.Find((MB3_AgglomerativeClustering.item_s x) => x.go == gos[i]) == null)
				{
					Renderer component = gos[i].GetComponent<Renderer>();
					if (component != null && (component is MeshRenderer || component is SkinnedMeshRenderer))
					{
						MB3_AgglomerativeClustering.item_s item_s = new MB3_AgglomerativeClustering.item_s();
						item_s.go = gos[i];
						item_s.coord = component.bounds.center;
						list.Add(item_s);
					}
				}
			}
			cluster.items = list;
			cluster.agglomerate(progFunc);
			if (!cluster.wasCanceled)
			{
				float smallest;
				float largest;
				_BuildListOfClustersToDraw(progFunc, out smallest, out largest);
				d.maxDistBetweenClusters = Mathf.Lerp(smallest, largest, 0.9f);
			}
		}

		private void _BuildListOfClustersToDraw(ProgressUpdateCancelableDelegate progFunc, out float smallest, out float largest)
		{
			_clustersToDraw.Clear();
			if (cluster.clusters == null)
			{
				smallest = 1f;
				largest = 10f;
				return;
			}
			if (progFunc != null)
			{
				progFunc("Building Clusters To Draw A:", 0f);
			}
			List<MB3_AgglomerativeClustering.ClusterNode> list = new List<MB3_AgglomerativeClustering.ClusterNode>();
			largest = 1f;
			smallest = 10000000f;
			for (int i = 0; i < cluster.clusters.Length; i++)
			{
				MB3_AgglomerativeClustering.ClusterNode clusterNode = cluster.clusters[i];
				if (clusterNode.distToMergedCentroid <= d.maxDistBetweenClusters)
				{
					if (d.includeCellsWithOnlyOneRenderer)
					{
						_clustersToDraw.Add(clusterNode);
					}
					else if (clusterNode.leaf == null)
					{
						_clustersToDraw.Add(clusterNode);
					}
				}
				if (clusterNode.distToMergedCentroid > largest)
				{
					largest = clusterNode.distToMergedCentroid;
				}
				if (clusterNode.height > 0 && clusterNode.distToMergedCentroid < smallest)
				{
					smallest = clusterNode.distToMergedCentroid;
				}
			}
			if (progFunc != null)
			{
				progFunc("Building Clusters To Draw B:", 0f);
			}
			for (int j = 0; j < _clustersToDraw.Count; j++)
			{
				list.Add(_clustersToDraw[j].cha);
				list.Add(_clustersToDraw[j].chb);
			}
			for (int k = 0; k < list.Count; k++)
			{
				_clustersToDraw.Remove(list[k]);
			}
			_radii = new float[_clustersToDraw.Count];
			if (progFunc != null)
			{
				progFunc("Building Clusters To Draw C:", 0f);
			}
			for (int l = 0; l < _radii.Length; l++)
			{
				MB3_AgglomerativeClustering.ClusterNode clusterNode2 = _clustersToDraw[l];
				Bounds bounds = new Bounds(clusterNode2.centroid, Vector3.one);
				for (int m = 0; m < clusterNode2.leafs.Length; m++)
				{
					Renderer component = cluster.clusters[clusterNode2.leafs[m]].leaf.go.GetComponent<Renderer>();
					if (component != null)
					{
						bounds.Encapsulate(component.bounds);
					}
				}
				_radii[l] = bounds.extents.magnitude;
			}
			if (progFunc != null)
			{
				progFunc("Building Clusters To Draw D:", 0f);
			}
			_ObjsExtents = largest + 1f;
			_minDistBetweenClusters = Mathf.Lerp(smallest, 0f, 0.9f);
			if (_ObjsExtents < 2f)
			{
				_ObjsExtents = 2f;
			}
		}

		public override void DrawGizmos(Bounds sceneObjectBounds)
		{
			if (cluster != null && cluster.clusters != null)
			{
				if (_lastMaxDistBetweenClusters != d.maxDistBetweenClusters)
				{
					float smallest;
					float largest;
					_BuildListOfClustersToDraw(null, out smallest, out largest);
					_lastMaxDistBetweenClusters = d.maxDistBetweenClusters;
				}
				for (int i = 0; i < _clustersToDraw.Count; i++)
				{
					Gizmos.color = Color.white;
					MB3_AgglomerativeClustering.ClusterNode clusterNode = _clustersToDraw[i];
					Gizmos.DrawWireSphere(clusterNode.centroid, _radii[i]);
				}
			}
		}
	}
}
