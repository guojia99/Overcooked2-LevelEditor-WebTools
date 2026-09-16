using UnityEngine;

[RequireComponent(typeof(IngredientContainer))]
public class IngredientCatcher : MonoBehaviour
{
	[SerializeField]
	public bool m_requireAttached = true;
}
