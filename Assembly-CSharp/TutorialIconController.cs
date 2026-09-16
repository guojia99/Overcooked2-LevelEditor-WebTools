using UnityEngine;

[RequireComponent(typeof(Animator))]
public class TutorialIconController : IconTutorialBase
{
	public enum TutorialStage
	{
		Lettuce = 0,
		LettuceTomato = 1,
		LettuceTomatoCucumber = 2
	}

	[SerializeField]
	public Transform m_plateStation;

	[SerializeField]
	public PlateReturnStation m_plateReturn;

	[SerializeField]
	public Transform[] m_choppingboards;

	[SerializeField]
	public Transform m_lettuceCrate;

	[SerializeField]
	public IngredientOrderNode m_lettuce;

	[SerializeField]
	public Transform m_tomatoCrate;

	[SerializeField]
	public IngredientOrderNode m_tomato;

	[SerializeField]
	public Transform m_cucumberCrate;

	[SerializeField]
	public IngredientOrderNode m_cucumber;

	[SerializeField]
	public Sprite m_lettuceSaladIcon;

	[SerializeField]
	public Sprite m_tomatoSaladIcon;

	[SerializeField]
	public Sprite m_cucumberSaladIcon;

	[SerializeField]
	[AssignComponent(Editorbility.NonEditable)]
	public Animator m_dialogueAnimator;

	[SerializeField]
	public TutorialPopup m_swapTutorialUI = new TutorialPopup();

	[SerializeField]
	public ControlPadInput.Button m_swapButton;
}
