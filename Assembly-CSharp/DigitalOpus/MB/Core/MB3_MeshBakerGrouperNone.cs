using System;
using System.Collections.Generic;
using UnityEngine;

namespace DigitalOpus.MB.Core
{
	[Serializable]
	public class MB3_MeshBakerGrouperNone : MB3_MeshBakerGrouperCore
	{
		public MB3_MeshBakerGrouperNone(GrouperData d)
		{
			base.d = d;
		}

		public override Dictionary<string, List<Renderer>> FilterIntoGroups(List<GameObject> selection)
		{
			Debug.Log("Filtering into groups none");
			Dictionary<string, List<Renderer>> dictionary = new Dictionary<string, List<Renderer>>();
			List<Renderer> list = new List<Renderer>();
			for (int i = 0; i < selection.Count; i++)
			{
				if (selection[i] != null)
				{
					list.Add(selection[i].GetComponent<Renderer>());
				}
			}
			dictionary.Add("MeshBaker", list);
			return dictionary;
		}

		public override void DrawGizmos(Bounds sourceObjectBounds)
		{
		}
	}
}
