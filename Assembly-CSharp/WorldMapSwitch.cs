using System;
using UnityEngine;

[RequireComponent(typeof(WorldMapFlipperBase))]
public class WorldMapSwitch : MonoBehaviour
{
	[Serializable]
	public class SwitchOwnerData
	{
		public SwitchMapNode m_switchMapNode;
	}

	[SerializeField]
	public SwitchOwnerData m_switchOwnerData = new SwitchOwnerData();

	[SerializeField]
	public MeshRenderer m_PadMesh;

	[SerializeField]
	public MeshRenderer m_PadGlowMesh;

	[SerializeField]
	public float m_PadPressDistance = 0.02f;

	public bool IsFlipped()
	{
		WorldMapFlipperBase worldMapFlipperBase = base.gameObject.RequireComponent<WorldMapFlipperBase>();
		return worldMapFlipperBase.IsFlipped();
	}

	public bool CanBePressed()
	{
		return m_switchOwnerData.m_switchMapNode == null || m_switchOwnerData.m_switchMapNode.CanProcessSwitch();
	}

	public bool IsSwitchActivated()
	{
		return m_switchOwnerData.m_switchMapNode == null || m_switchOwnerData.m_switchMapNode.IsSwitchPressed();
	}
}
