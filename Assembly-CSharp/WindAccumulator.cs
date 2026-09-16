using System.Collections.Generic;
using UnityEngine;

public class WindAccumulator : MonoBehaviour, IWindReceiver
{
	private static List<WindAccumulator> s_allWindReceivers = new List<WindAccumulator>();

	private List<IWindSource> m_sources = new List<IWindSource>();

	private Vector3 m_totalForce = Vector3.zero;

	public static List<WindAccumulator> GetAllWindReceivers()
	{
		return s_allWindReceivers;
	}

	private void OnEnable()
	{
		s_allWindReceivers.Add(this);
	}

	private void OnDisable()
	{
		m_sources.Clear();
		s_allWindReceivers.Remove(this);
	}

	protected virtual void Start()
	{
	}

	protected virtual void Update()
	{
		m_totalForce = Vector3.zero;
		for (int i = 0; i < m_sources.Count; i++)
		{
			m_totalForce += m_sources[i].GetVelocity();
		}
	}

	public virtual void AddWindSource(IWindSource _source)
	{
		if (!m_sources.Contains(_source))
		{
			m_sources.Add(_source);
		}
	}

	public virtual void RemoveWindSource(IWindSource _source)
	{
		m_sources.Remove(_source);
	}

	public void Reset()
	{
		m_sources.Clear();
	}

	public Vector3 GetVelocity()
	{
		return m_totalForce;
	}
}
