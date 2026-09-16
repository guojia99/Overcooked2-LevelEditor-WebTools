using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class MeshVisibilityBase<StateEnum> : MonoBehaviour where StateEnum : struct, IConvertible
{
	[SerializeField]
	public string[] m_meshes = new string[0];

	[HideInInspector]
	[SerializeField]
	public int[] m_stateFlags = new int[0];

	private Dictionary<string, Renderer> m_renderers = new Dictionary<string, Renderer>();

	private StateEnum m_state;

	private StateEnum[] m_stateValues;

	public void Setup(StateEnum _state)
	{
		m_stateValues = (StateEnum[])Enum.GetValues(typeof(StateEnum));
		Renderer[] renderers = base.gameObject.RequestComponentsRecursive<Renderer>();
		m_renderers.Clear();
		for (int i = 0; i < m_meshes.Length; i++)
		{
			Renderer renderer = FindMesh(m_meshes[i], renderers);
			if (renderer != null)
			{
				m_renderers.Add(m_meshes[i], renderer);
			}
		}
		SetState(_state);
	}

	public void SetState(StateEnum _state)
	{
		int num = 0;
		for (int i = 0; i < m_stateValues.Length; i++)
		{
			if (m_stateValues[i].Equals(_state))
			{
				int num2 = ((m_stateFlags.Length > num) ? m_stateFlags[num] : 0);
				for (int j = 0; j < m_meshes.Length; j++)
				{
					bool visibility = (num2 & (1 << j)) != 0;
					SetMeshVisibility(m_meshes[j], visibility);
				}
			}
			num++;
		}
		m_state = _state;
	}

	private void SetMeshVisibility(string _mesh, bool _visibility)
	{
		if (m_renderers.ContainsKey(_mesh))
		{
			Renderer renderer = m_renderers[_mesh];
			renderer.enabled = _visibility;
		}
	}

	protected virtual Renderer FindMesh(string _name, Renderer[] renderers = null)
	{
		if (renderers == null)
		{
			renderers = base.gameObject.RequestComponentsRecursive<Renderer>();
		}
		foreach (Renderer renderer in renderers)
		{
			if (renderer.name.Equals(_name))
			{
				return renderer;
			}
		}
		return null;
	}
}
