using System;
using UnityEngine;

namespace DigitalOpus.MB.Core
{
	[Serializable]
	public class GrouperData
	{
		public bool clusterOnLMIndex;

		public bool clusterByLODLevel;

		public Vector3 origin;

		public Vector3 cellSize;

		public int pieNumSegments = 4;

		public Vector3 pieAxis = Vector3.up;

		public int height = 1;

		public float maxDistBetweenClusters = 1f;

		public bool includeCellsWithOnlyOneRenderer = true;
	}
}
