using System.Collections.Generic;
using UnityEngine;

public class SuppressionController
{
	private List<Suppressor> m_suppressors = new List<Suppressor>();

	public bool IsSuppressed()
	{
		return m_suppressors.Count != 0;
	}

	public void UpdateSuppressors()
	{
		m_suppressors.RemoveAll((Suppressor x) => x.IsReleased());
	}

	public Suppressor AddSuppressor(Object _owner)
	{
		if (_owner == null)
		{
			return null;
		}
		Suppressor suppressor = new Suppressor(_owner);
		m_suppressors.Add(suppressor);
		return suppressor;
	}

	public void Reset()
	{
		for (int i = 0; i < m_suppressors.Count; i++)
		{
			m_suppressors[i].Release();
		}
		m_suppressors.Clear();
	}

	public void MoveSuppressors(SuppressionController suppressionController)
	{
		suppressionController.m_suppressors.AddRange(m_suppressors);
		m_suppressors.Clear();
	}
}
