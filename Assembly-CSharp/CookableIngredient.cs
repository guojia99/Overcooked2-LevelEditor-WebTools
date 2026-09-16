using UnityEngine;

[AddComponentMenu("Scripts/Game/Environment/CookableIngredient")]
[RequireComponent(typeof(CookingHandler))]
public class CookableIngredient : MonoBehaviour
{
	[SerializeField]
	public IngredientOrderNode m_ingredientOrderNode;
}
