using System.Collections.Generic;
using UnityEngine;

public class TriggerRecorder : MonoBehaviour
{
	private List<Collider> m_triggers = new List<Collider>();

	private void OnTriggerStay(Collider collider)
	{
		m_triggers.Add(collider);
	}

	private void FixedUpdate()
	{
		m_triggers.Clear();
	}

	public List<Collider> GetRecentCollisions()
	{
		return m_triggers;
	}
}
