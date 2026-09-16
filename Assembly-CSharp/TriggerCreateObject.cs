using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Scripts/Core/Components/TriggerCreateObject")]
public class TriggerCreateObject : MonoBehaviour
{
	[SerializeField]
	private GameObject m_objectToCreate;

	[SerializeField]
	private string m_trigger;

	[SerializeField]
	private int m_maxNumber = 100;

	private List<GameObject> m_spawned = new List<GameObject>();

	private void OnTrigger(string _trigger)
	{
		if (m_trigger == _trigger)
		{
			m_spawned.RemoveAll((GameObject obj) => obj == null);
			if (m_spawned.Count < m_maxNumber)
			{
				GameObject gameObject = m_objectToCreate.Instantiate(base.transform.position, base.transform.rotation);
				gameObject.name = m_objectToCreate.name;
				m_spawned.Add(gameObject);
			}
		}
	}
}
