using LevelEditor;
using UnityEngine;

[ExecutionDependency(typeof(IFlowController))]
[AddComponentMenu("Scripts/Game/Environment/CookingStation")]
[RequireComponent(typeof(AttachStation))]
public class CookingStation : MonoBehaviour
{
	[SerializeField]
	public GameObject m_flameEffect;

	[SerializeField]
	public CookingStationType m_stationType;

	[SerializeField]
	public Collider m_itemBlock;

	[SerializeField]
	public bool m_attachRestrictions = true;

	public void SetCookerOn(bool _isOn)
	{
		if (m_flameEffect != null)
		{
			m_flameEffect.SetActive(_isOn);
		}
	}

	public virtual bool CanAddItem(GameObject _object, MixedCompositeOrderNode.MixingProgress? mixingProgress)
	{
		if (!m_attachRestrictions)
		{
			return true;
		}
		IBaseCookable baseCookable = _object.RequestInterface<IBaseCookable>();
		if (baseCookable == null)
		{
			return false;
		}
		// patch
		//if (baseCookable.GetRequiredStationType() != m_stationType)
		//{
		//	return false;
		//}
        if (baseCookable.GetRequiredStationType() != m_stationType)
        {
            Component component = baseCookable as Component;
            if (component == null) return false;
            MultiCookingStationTypes multiCookingStationTypes = component.GetComponent<MultiCookingStationTypes>();
            if (multiCookingStationTypes == null || !multiCookingStationTypes.MatchType(m_stationType))
                return false;
        }
        // patch
        IIngredientContents ingredientContents = _object.RequestInterface<IIngredientContents>();
		if (ingredientContents != null && !ingredientContents.HasContents())
		{
			return true;
		}
		if (mixingProgress.HasValue && mixingProgress.Value != MixedCompositeOrderNode.MixingProgress.Mixed)
		{
			return false;
		}
		return true;
	}
}
