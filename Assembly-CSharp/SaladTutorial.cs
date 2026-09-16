using UnityEngine;

[AddComponentMenu("Scripts/Game/Flow/Tutorials/Salad Tutorial")]
public class SaladTutorial : IconTutorialBase
{
	[SerializeField]
	protected Transform m_tomatoCrate;

	[SerializeField]
	protected Transform m_lettuceCrate;

	[SerializeField]
	protected Transform[] m_choppingboards;

	[SerializeField]
	protected Transform m_plate;

	[SerializeField]
	protected Transform m_plateStation;

	[SerializeField]
	protected IngredientOrderNode m_tomato;

	[SerializeField]
	protected IngredientOrderNode m_lettuce;

	[SerializeField]
	protected Sprite m_saladIcon;

	[SerializeField]
	[AssignComponent(Editorbility.NonEditable)]
	private Animator m_dialogueAnimator;
}
