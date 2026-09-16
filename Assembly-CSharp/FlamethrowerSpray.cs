using UnityEngine;

public class FlamethrowerSpray : SprayingUtensil
{
	[SerializeField]
	public float m_cookingRate = 2f;

	[SerializeField]
	public float m_smoulderTime = 5f;

	[SerializeField]
	public GameObject m_smoulderEffect;
}
