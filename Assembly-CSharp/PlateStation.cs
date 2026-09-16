using System;
using UnityEngine;

[AddComponentMenu("Scripts/Game/Environment/PlateStation")]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(AttachStation))]
public class PlateStation : MonoBehaviour
{
	[Serializable]
	public class DeliveryFX
	{
		public GameObject m_deliverPFXPrefab;

		public float m_pfxToFadeDelayTime = 0.2f;

		public Shader m_fadeOutShader;

		public float m_fadeTime = 0.5f;
	}

	[SerializeField]
	[AssignResource("NeedsPlateFloatingUI", Editorbility.NonEditable)]
	public GameObject m_needsPlateFloatingUI;

	[SerializeField]
	[AssignResource("TipsFloatingNumberUI", Editorbility.NonEditable)]
	public GameObject m_tipsFloatingNumberUI;

	[SerializeField]
	public PlateReturnStation[] m_returnStations;

	[SerializeField]
	public GameObject m_platePrefab;

	[SerializeField]
	public float m_createPlateTime;

	[SerializeField]
	public TeamID m_teamId;

	[SerializeField]
	public string m_onFoodDeliveredTrigger;

	[SerializeField]
	public DeliveryFX m_deliveryEffects = new DeliveryFX();
}
