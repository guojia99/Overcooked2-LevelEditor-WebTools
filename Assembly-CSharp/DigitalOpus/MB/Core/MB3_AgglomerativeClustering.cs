using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace DigitalOpus.MB.Core
{
	[Serializable]
	public class MB3_AgglomerativeClustering
	{
		[Serializable]
		public class ClusterNode
		{
			public item_s leaf;

			public ClusterNode cha;

			public ClusterNode chb;

			public int height;

			public float distToMergedCentroid;

			public Vector3 centroid;

			public int[] leafs;

			public int idx;

			public bool isUnclustered = true;

			public ClusterNode(item_s ii, int index)
			{
				leaf = ii;
				idx = index;
				leafs = new int[1];
				leafs[0] = index;
				centroid = ii.coord;
				height = 0;
			}

			public ClusterNode(ClusterNode a, ClusterNode b, int index, int h, float dist, ClusterNode[] clusters)
			{
				cha = a;
				chb = b;
				idx = index;
				leafs = new int[a.leafs.Length + b.leafs.Length];
				Array.Copy(a.leafs, leafs, a.leafs.Length);
				Array.Copy(b.leafs, 0, leafs, a.leafs.Length, b.leafs.Length);
				Vector3 zero = Vector3.zero;
				for (int i = 0; i < leafs.Length; i++)
				{
					zero += clusters[leafs[i]].centroid;
				}
				centroid = zero / leafs.Length;
				height = h;
				distToMergedCentroid = dist;
			}
		}

		[Serializable]
		public class item_s
		{
			public GameObject go;

			public Vector3 coord;
		}

		public class ClusterDistance
		{
			public ClusterNode a;

			public ClusterNode b;

			public ClusterDistance(ClusterNode aa, ClusterNode bb)
			{
				a = aa;
				b = bb;
			}
		}

		public List<item_s> items = new List<item_s>();

		public ClusterNode[] clusters;

		public bool wasCanceled;

		private const int MAX_PRIORITY_Q_SIZE = 2048;

		private float euclidean_distance(Vector3 a, Vector3 b)
		{
			return Vector3.Distance(a, b);
		}

		public bool agglomerate(ProgressUpdateCancelableDelegate progFunc)
		{
			wasCanceled = true;
			if (progFunc != null)
			{
				wasCanceled = progFunc("Filling Priority Queue:", 0f);
			}
			if (items.Count <= 1)
			{
				clusters = new ClusterNode[0];
				return false;
			}
			clusters = new ClusterNode[items.Count * 2 - 1];
			for (int i = 0; i < items.Count; i++)
			{
				clusters[i] = new ClusterNode(items[i], i);
			}
			int num = items.Count;
			List<ClusterNode> list = new List<ClusterNode>();
			for (int j = 0; j < num; j++)
			{
				clusters[j].isUnclustered = true;
				list.Add(clusters[j]);
			}
			int num2 = 0;
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();
			float num3 = 0f;
			long num4 = GC.GetTotalMemory(false) / 1000000;
			PriorityQueue<float, ClusterDistance> priorityQueue = new PriorityQueue<float, ClusterDistance>();
			int num5 = 0;
			while (list.Count > 1)
			{
				int num6 = 0;
				num2++;
				if (priorityQueue.Count == 0)
				{
					num5++;
					num4 = GC.GetTotalMemory(false) / 1000000;
					if (progFunc != null)
					{
						wasCanceled = progFunc("Refilling Q:" + (float)(items.Count - list.Count) * 100f / (float)items.Count + " unclustered:" + list.Count + " inQ:" + priorityQueue.Count + " usedMem:" + num4, (float)(items.Count - list.Count) / (float)items.Count);
					}
					num3 = _RefillPriorityQWithSome(priorityQueue, list, clusters, progFunc);
					if (priorityQueue.Count == 0)
					{
						break;
					}
				}
				KeyValuePair<float, ClusterDistance> keyValuePair = priorityQueue.Dequeue();
				while (!keyValuePair.Value.a.isUnclustered || !keyValuePair.Value.b.isUnclustered)
				{
					if (priorityQueue.Count == 0)
					{
						num5++;
						num4 = GC.GetTotalMemory(false) / 1000000;
						if (progFunc != null)
						{
							wasCanceled = progFunc("Creating clusters:" + (float)(items.Count - list.Count) * 100f / (float)items.Count + " unclustered:" + list.Count + " inQ:" + priorityQueue.Count + " usedMem:" + num4, (float)(items.Count - list.Count) / (float)items.Count);
						}
						num3 = _RefillPriorityQWithSome(priorityQueue, list, clusters, progFunc);
						if (priorityQueue.Count == 0)
						{
							break;
						}
					}
					keyValuePair = priorityQueue.Dequeue();
					num6++;
				}
				num++;
				ClusterNode clusterNode = new ClusterNode(keyValuePair.Value.a, keyValuePair.Value.b, num - 1, num2, keyValuePair.Key, clusters);
				list.Remove(keyValuePair.Value.a);
				list.Remove(keyValuePair.Value.b);
				keyValuePair.Value.a.isUnclustered = false;
				keyValuePair.Value.b.isUnclustered = false;
				int num7 = num - 1;
				if (num7 == clusters.Length)
				{
					UnityEngine.Debug.LogError("how did this happen");
				}
				clusters[num7] = clusterNode;
				list.Add(clusterNode);
				clusterNode.isUnclustered = true;
				for (int k = 0; k < list.Count - 1; k++)
				{
					float num8 = euclidean_distance(clusterNode.centroid, list[k].centroid);
					if (num8 < num3)
					{
						priorityQueue.Add(new KeyValuePair<float, ClusterDistance>(num8, new ClusterDistance(clusterNode, list[k])));
					}
				}
				if (wasCanceled)
				{
					break;
				}
				num4 = GC.GetTotalMemory(false) / 1000000;
				if (progFunc != null)
				{
					wasCanceled = progFunc("Creating clusters:" + (float)(items.Count - list.Count) * 100f / (float)items.Count + " unclustered:" + list.Count + " inQ:" + priorityQueue.Count + " usedMem:" + num4, (float)(items.Count - list.Count) / (float)items.Count);
				}
			}
			if (progFunc != null)
			{
				wasCanceled = progFunc("Finished clustering:", 100f);
			}
			if (wasCanceled)
			{
				return false;
			}
			return true;
		}

		private float _RefillPriorityQWithSome(PriorityQueue<float, ClusterDistance> pq, List<ClusterNode> unclustered, ClusterNode[] clusters, ProgressUpdateCancelableDelegate progFunc)
		{
			List<float> list = new List<float>(2048);
			for (int i = 0; i < unclustered.Count; i++)
			{
				for (int j = i + 1; j < unclustered.Count; j++)
				{
					list.Add(euclidean_distance(unclustered[i].centroid, unclustered[j].centroid));
				}
				wasCanceled = progFunc("Refilling Queue Part A:", (float)i / ((float)unclustered.Count * 2f));
				if (wasCanceled)
				{
					return 10f;
				}
			}
			if (list.Count == 0)
			{
				return 1E+11f;
			}
			float num = NthSmallestElement(list, 2048);
			for (int k = 0; k < unclustered.Count; k++)
			{
				for (int l = k + 1; l < unclustered.Count; l++)
				{
					int idx = unclustered[k].idx;
					int idx2 = unclustered[l].idx;
					float num2 = euclidean_distance(unclustered[k].centroid, unclustered[l].centroid);
					if (num2 <= num)
					{
						pq.Add(new KeyValuePair<float, ClusterDistance>(num2, new ClusterDistance(clusters[idx], clusters[idx2])));
					}
				}
				wasCanceled = progFunc("Refilling Queue Part B:", (float)(unclustered.Count + k) / ((float)unclustered.Count * 2f));
				if (wasCanceled)
				{
					return 10f;
				}
			}
			return num;
		}

		public int TestRun(List<GameObject> gos)
		{
			List<item_s> list = new List<item_s>();
			for (int i = 0; i < gos.Count; i++)
			{
				item_s item_s2 = new item_s();
				item_s2.go = gos[i];
				item_s2.coord = gos[i].transform.position;
				list.Add(item_s2);
			}
			items = list;
			if (items.Count > 0)
			{
				agglomerate(null);
			}
			return 0;
		}

		public static void Main()
		{
			List<float> list = new List<float>();
			list.AddRange(new float[10] { 19f, 18f, 17f, 16f, 15f, 10f, 11f, 12f, 13f, 14f });
			UnityEngine.Debug.Log("Loop quick select 10 times.");
			UnityEngine.Debug.Log(NthSmallestElement(list, 0));
		}

		public static T NthSmallestElement<T>(List<T> array, int n) where T : IComparable<T>
		{
			if (n < 0)
			{
				n = 0;
			}
			if (n > array.Count - 1)
			{
				n = array.Count - 1;
			}
			if (array.Count == 0)
			{
				throw new ArgumentException("Array is empty.", "array");
			}
			if (array.Count == 1)
			{
				return array[0];
			}
			return QuickSelectSmallest(array, n)[n];
		}

		private static List<T> QuickSelectSmallest<T>(List<T> input, int n) where T : IComparable<T>
		{
			int num = 0;
			int num2 = input.Count - 1;
			int pivotIndex = n;
			System.Random random = new System.Random();
			while (num2 > num)
			{
				pivotIndex = QuickSelectPartition(input, num, num2, pivotIndex);
				if (pivotIndex == n)
				{
					break;
				}
				if (pivotIndex > n)
				{
					num2 = pivotIndex - 1;
				}
				else
				{
					num = pivotIndex + 1;
				}
				pivotIndex = random.Next(num, num2);
			}
			return input;
		}

		private static int QuickSelectPartition<T>(List<T> array, int startIndex, int endIndex, int pivotIndex) where T : IComparable<T>
		{
			T other = array[pivotIndex];
			Swap(array, pivotIndex, endIndex);
			for (int i = startIndex; i < endIndex; i++)
			{
				if (array[i].CompareTo(other) <= 0)
				{
					Swap(array, i, startIndex);
					startIndex++;
				}
			}
			Swap(array, endIndex, startIndex);
			return startIndex;
		}

		private static void Swap<T>(List<T> array, int index1, int index2)
		{
			if (index1 != index2)
			{
				T value = array[index1];
				array[index1] = array[index2];
				array[index2] = value;
			}
		}
	}
}
