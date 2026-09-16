using System.Collections.Generic;
using UnityEngine;

public class DebugMenu : DebugRootMenu
{
	[SerializeField]
	public GameObject m_container;

	[SerializeField]
	public GameObject m_prefab;

	[SerializeField]
	private T17ScrollView m_scrollView;

	private List<GameObject> m_entrys = new List<GameObject>();

	public override bool Show(GamepadUser currentGamer, BaseMenuBehaviour parent, GameObject invoker, bool hideInvoker = true)
	{
		if (!base.Show(currentGamer, parent, invoker, hideInvoker))
		{
			return false;
		}
		IT17EventHelper[] componentsInChildren = GetComponentsInChildren<IT17EventHelper>(true);
		Dictionary<string, bool> options = DebugManager.Instance.GetOptions();
		foreach (KeyValuePair<string, bool> item in options)
		{
			GameObject gameObject = Object.Instantiate(m_prefab);
			m_entrys.Add(gameObject);
			DebugMenuButton component = gameObject.GetComponent<DebugMenuButton>();
			component.SetName(item.Key);
			component.SetStatus(item.Value);
			gameObject.transform.SetParent(m_container.transform);
		}
		m_scrollView.Show(currentGamer, parent, invoker, hideInvoker);
		return true;
	}

	public override void Close()
	{
		base.Close();
		for (int i = 0; i < m_entrys.Count; i++)
		{
			Object.Destroy(m_entrys[i]);
		}
		m_entrys.Clear();
		m_scrollView.Hide();
	}
}
