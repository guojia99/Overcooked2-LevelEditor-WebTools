using System;
using System.Collections.Generic;
using UnityEngine;

public class MultiTriggerAdapter : MonoBehaviour
{
	[Serializable]
	public class Adapter
	{
		[SerializeField]
		public string m_inputTrigger;

		[SerializeField]
		public string m_outputTrigger;
	}

	[SerializeField]
	public List<Adapter> m_adapters = new List<Adapter>();
}
