using UnityEngine;

[AddComponentMenu("Scripts/Game/Flow/Tutorials/Soup Tutorial")]
public class SoupTutorial : IconTutorialBase
{
	[SerializeField]
	private Sprite m_potIcon;

	[SerializeField]
	private Sprite m_soupIcon;

	[SerializeField]
	private IngredientOrderNode m_onion;

	[SerializeField]
	private CookedCompositeOrderNode m_onionSoup;

	[SerializeField]
	private GameObject m_cleanPlatesTutorialUI;
}
