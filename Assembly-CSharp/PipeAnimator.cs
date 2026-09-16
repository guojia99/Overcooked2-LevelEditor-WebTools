using System;
using System.Collections.Generic;
using UnityEngine;

public class PipeAnimator : MonoBehaviour, ISerializationCallbackReceiver
{
	[Serializable]
	private struct IntRange
	{
		public int m_first;

		public int m_count;
	}

	[SerializeField]
	private PipeAnimatorData[] m_datas = new PipeAnimatorData[0];

	[SerializeField]
	private string m_materialPipePosPropertyString = "_PipePos";

	[SerializeField]
	private string m_materialPipeParamsPropertyString = "_PipeParams";

	private MaterialPropertyBlock[] m_propertyBlocks = new MaterialPropertyBlock[0];

	[ReadOnly]
	[SerializeField]
	private Renderer[] m_renderers = new Renderer[0];

	[ReadOnly]
	[SerializeField]
	private IntRange[] m_rendererRanges = new IntRange[0];

	private void Awake()
	{
		m_propertyBlocks = new MaterialPropertyBlock[m_datas.Length];
		for (int i = 0; i < m_propertyBlocks.Length; i++)
		{
			m_propertyBlocks[i] = new MaterialPropertyBlock();
		}
	}

	private void Update()
	{
		PipeAnimatorData pipeAnimatorData = null;
		for (int i = 0; i < m_datas.Length; i++)
		{
			pipeAnimatorData = m_datas[i];
			Vector3 vector = pipeAnimatorData.m_path.Evaluate(Mathf.Clamp01(pipeAnimatorData.m_position));
			m_propertyBlocks[i].Clear();
			m_propertyBlocks[i].SetVector(m_materialPipePosPropertyString, vector);
			m_propertyBlocks[i].SetVector(m_materialPipeParamsPropertyString, new Vector4(pipeAnimatorData.m_falloff, pipeAnimatorData.m_displacement, 0f, 0f));
		}
		for (int j = 0; j < m_rendererRanges.Length; j++)
		{
			IntRange intRange = m_rendererRanges[j];
			int num = intRange.m_first + intRange.m_count;
			for (int k = intRange.m_first; k < num; k++)
			{
				m_renderers[k].SetPropertyBlock(m_propertyBlocks[j]);
			}
		}
	}

	public void OnAfterDeserialize()
	{
	}

	public void OnBeforeSerialize()
	{
		List<Renderer> list = new List<Renderer>();
		m_rendererRanges = new IntRange[m_datas.Length];
		PipeAnimatorData pipeAnimatorData = null;
		int num = 0;
		while (m_datas != null && num < m_datas.Length)
		{
			pipeAnimatorData = m_datas[num];
			if (pipeAnimatorData != null)
			{
				m_rendererRanges[num].m_first = list.Count;
				Renderer renderer = pipeAnimatorData.gameObject.RequestComponent<Renderer>();
				if (renderer != null)
				{
					list.Add(renderer);
				}
				int childCount = m_datas[num].transform.childCount;
				for (int i = 0; i < childCount; i++)
				{
					Transform child = m_datas[num].transform.GetChild(i);
					renderer = child.gameObject.RequestComponent<Renderer>();
					if (renderer != null)
					{
						list.Add(renderer);
					}
				}
				m_rendererRanges[num].m_count = Mathf.Max(0, list.Count - m_rendererRanges[num].m_first);
			}
			num++;
		}
		m_renderers = list.ToArray();
	}
}
