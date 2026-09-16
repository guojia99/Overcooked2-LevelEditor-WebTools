using System;
using UnityEngine;

public class GarbageCollectionManager : MonoBehaviour
{
	[SerializeField]
	private int m_framesBeforeCollection = 30;

	private void Update()
	{
		if (Time.frameCount % m_framesBeforeCollection == 0)
		{
			GC.Collect();
		}
	}
}
