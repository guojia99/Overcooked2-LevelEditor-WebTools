using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class AnimationInspectionBase : MonoBehaviour
{
	private struct DatastreamData
	{
		public int m_nameHash;

		public string m_streamName;

		public FloatDatastreamCalculator m_calculator;
	}

	protected delegate float FloatDatastreamCalculator(float _animPropThrough);

	[SerializeField]
	[AssignComponentRecursive(Visibility.Show)]
	public Animator m_animator;

	private DatastreamData[] m_datastreams = new DatastreamData[0];

	private Dictionary<string, bool> m_streamNameValidator = new Dictionary<string, bool>();

	protected void AddDatastream(string _animationName, string _datastreamName, FloatDatastreamCalculator _calculator)
	{
		DatastreamData datastreamData = default(DatastreamData);
		datastreamData.m_nameHash = Animator.StringToHash(_animationName);
		datastreamData.m_streamName = _datastreamName;
		datastreamData.m_calculator = _calculator;
		Array.Resize(ref m_datastreams, m_datastreams.Length + 1);
		m_datastreams[m_datastreams.Length - 1] = datastreamData;
		if (!m_streamNameValidator.ContainsKey(_datastreamName))
		{
			m_streamNameValidator.Add(_datastreamName, true);
		}
	}

	public float GetDatastreamFloat(string _id)
	{
		float result = 0f;
		for (int i = 0; i < m_animator.layerCount; i++)
		{
			AnimatorStateInfo currentAnimatorStateInfo = m_animator.GetCurrentAnimatorStateInfo(i);
			int nNameHash = currentAnimatorStateInfo.nameHash;
			DatastreamData[] array = Array.FindAll(m_datastreams, (DatastreamData _d) => _d.m_nameHash == nNameHash && _d.m_streamName == _id);
			DatastreamData[] array2 = array;
			for (int num = 0; num < array2.Length; num++)
			{
				DatastreamData datastreamData = array2[num];
				result = Mathf.Max(datastreamData.m_calculator(currentAnimatorStateInfo.normalizedTime));
			}
		}
		return result;
	}
}
