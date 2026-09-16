using System.Collections.Generic;
using UnityEngine;

public static class WorldMapMaterialPool
{
	public enum MapState
	{
		Folded = 0,
		UnFolded = 1
	}

	private static FastList<Material> m_MaterialPool;

	private static Material m_FoldedMaterial;

	public static void RegisterMaterial(Material material)
	{
		if (m_MaterialPool == null)
		{
			m_MaterialPool = new FastList<Material>();
		}
		if (m_FoldedMaterial == null && material.shader.name.Contains("lerp"))
		{
			m_FoldedMaterial = new Material(material);
			m_FoldedMaterial.SetFloat("_Blend", 0f);
		}
		if (m_MaterialPool.Find((Material x) => x.name.GetHashCode() == material.name.GetHashCode()) == null)
		{
			Material material2 = new Material(material);
			material2.SetFloat("_Blend", 1f);
			m_MaterialPool.Add(material2);
		}
	}

	public static Material GetSharedMaterialForState(Material orignalMaterial, MapState state)
	{
		if (m_MaterialPool == null)
		{
			RegisterMaterial(orignalMaterial);
		}
		if (state == MapState.Folded)
		{
			if (!orignalMaterial.shader.name.Contains("lerp"))
			{
				return orignalMaterial;
			}
			return m_FoldedMaterial;
		}
		int count = m_MaterialPool.Count;
		for (int i = 0; i < count; i++)
		{
			Material material = m_MaterialPool._items[i];
			if (material.name.GetHashCode() == orignalMaterial.name.GetHashCode())
			{
				return material;
			}
		}
		RegisterMaterial(orignalMaterial);
		count = m_MaterialPool.Count;
		for (int j = 0; j < count; j++)
		{
			Material material2 = m_MaterialPool._items[j];
			if (material2.name.GetHashCode() == orignalMaterial.name.GetHashCode())
			{
				return material2;
			}
		}
		return null;
	}
}
