using System.Collections.Generic;
using UnityEngine;

public class MB3_KMeansClustering
{
	private class DataPoint
	{
		public Vector3 center;

		public GameObject gameObject;

		public int Cluster;

		public DataPoint(GameObject go)
		{
			gameObject = go;
			center = go.transform.position;
			if (go.GetComponent<Renderer>() == null)
			{
				Debug.LogError("Object does not have a renderer " + go);
			}
		}
	}

	private List<DataPoint> _normalizedDataToCluster = new List<DataPoint>();

	private Vector3[] _clusters = new Vector3[0];

	private int _numberOfClusters;

	public MB3_KMeansClustering(List<GameObject> gos, int numClusters)
	{
		for (int i = 0; i < gos.Count; i++)
		{
			if (gos[i] != null)
			{
				DataPoint item = new DataPoint(gos[i]);
				_normalizedDataToCluster.Add(item);
			}
			else
			{
				Debug.LogWarning(string.Format("Object {0} in list of objects to cluster was null.", i));
			}
		}
		if (numClusters <= 0)
		{
			Debug.LogError("Number of clusters must be posititve.");
			numClusters = 1;
		}
		if (_normalizedDataToCluster.Count <= numClusters)
		{
			Debug.LogError("There must be fewer clusters than objects to cluster");
			numClusters = _normalizedDataToCluster.Count - 1;
		}
		_numberOfClusters = numClusters;
		if (_numberOfClusters <= 0)
		{
			_numberOfClusters = 1;
		}
		_clusters = new Vector3[_numberOfClusters];
	}

	private void InitializeCentroids()
	{
		for (int i = 0; i < _numberOfClusters; i++)
		{
			_normalizedDataToCluster[i].Cluster = i;
		}
		for (int j = _numberOfClusters; j < _normalizedDataToCluster.Count; j++)
		{
			_normalizedDataToCluster[j].Cluster = Random.Range(0, _numberOfClusters);
		}
	}

	private bool UpdateDataPointMeans(bool force)
	{
		if (AnyAreEmpty(_normalizedDataToCluster) && !force)
		{
			return false;
		}
		Vector3[] array = new Vector3[_numberOfClusters];
		int[] array2 = new int[_numberOfClusters];
		for (int i = 0; i < _normalizedDataToCluster.Count; i++)
		{
			int cluster = _normalizedDataToCluster[i].Cluster;
			array[cluster] += _normalizedDataToCluster[i].center;
			array2[cluster]++;
		}
		for (int j = 0; j < _numberOfClusters; j++)
		{
			_clusters[j] = array[j] / array2[j];
		}
		return true;
	}

	private bool AnyAreEmpty(List<DataPoint> data)
	{
		int[] array = new int[_numberOfClusters];
		for (int i = 0; i < _normalizedDataToCluster.Count; i++)
		{
			array[_normalizedDataToCluster[i].Cluster]++;
		}
		for (int j = 0; j < array.Length; j++)
		{
			if (array[j] == 0)
			{
				return true;
			}
		}
		return false;
	}

	private bool UpdateClusterMembership()
	{
		bool flag = false;
		float[] array = new float[_numberOfClusters];
		for (int i = 0; i < _normalizedDataToCluster.Count; i++)
		{
			for (int j = 0; j < _numberOfClusters; j++)
			{
				array[j] = ElucidanDistance(_normalizedDataToCluster[i], _clusters[j]);
			}
			int num = MinIndex(array);
			if (num != _normalizedDataToCluster[i].Cluster)
			{
				flag = true;
				_normalizedDataToCluster[i].Cluster = num;
			}
		}
		if (!flag)
		{
			return false;
		}
		return true;
	}

	private float ElucidanDistance(DataPoint dataPoint, Vector3 mean)
	{
		return Vector3.Distance(dataPoint.center, mean);
	}

	private int MinIndex(float[] distances)
	{
		int result = 0;
		double num = distances[0];
		for (int i = 0; i < distances.Length; i++)
		{
			if ((double)distances[i] < num)
			{
				num = distances[i];
				result = i;
			}
		}
		return result;
	}

	public List<Renderer> GetCluster(int idx, out Vector3 mean, out float size)
	{
		if (idx < 0 || idx >= _numberOfClusters)
		{
			Debug.LogError("idx is out of bounds");
			mean = Vector3.zero;
			size = 1f;
			return new List<Renderer>();
		}
		UpdateDataPointMeans(true);
		List<Renderer> list = new List<Renderer>();
		mean = _clusters[idx];
		float num = 0f;
		for (int i = 0; i < _normalizedDataToCluster.Count; i++)
		{
			if (_normalizedDataToCluster[i].Cluster == idx)
			{
				float num2 = Vector3.Distance(mean, _normalizedDataToCluster[i].center);
				if (num2 > num)
				{
					num = num2;
				}
				list.Add(_normalizedDataToCluster[i].gameObject.GetComponent<Renderer>());
			}
		}
		mean = _clusters[idx];
		size = num;
		return list;
	}

	public void Cluster()
	{
		bool flag = true;
		bool flag2 = true;
		InitializeCentroids();
		int num = _normalizedDataToCluster.Count * 1000;
		int num2 = 0;
		while (flag2 && flag && num2 < num)
		{
			num2++;
			flag2 = UpdateDataPointMeans(false);
			flag = UpdateClusterMembership();
		}
	}
}
