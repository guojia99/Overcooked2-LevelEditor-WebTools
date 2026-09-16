using UnityEngine;

[ExecutionDependency(typeof(IOrderDefinition))]
public abstract class MealCosmeticDecisions : MonoBehaviour
{
	protected GameObject m_container;

	protected IClientOrderDefinition m_iOrderDefinition;

	private RendererSceneInfo m_rendererSceneInfo;

	protected abstract IClientOrderDefinition FindOrderDefinition();

	protected abstract void UpdateAppearance(AssembledDefinitionNode _contents);

	protected virtual void Start()
	{
		m_iOrderDefinition = FindOrderDefinition();
		m_iOrderDefinition.RegisterOrderCompositionChangedCallback(OnOrderCompositionChanged);
		m_container = GameObjectUtils.CreateOnParent(base.gameObject, "IngredientContainer");
		m_rendererSceneInfo = base.gameObject.RequestComponent<RendererSceneInfo>();
		if (m_rendererSceneInfo == null)
		{
			m_rendererSceneInfo = base.gameObject.AddComponent<RendererSceneInfo>();
			m_rendererSceneInfo.m_rendererClass = RendererSceneSettings.RendererClass.MealCosmetic;
		}
		OnOrderCompositionChanged(m_iOrderDefinition.GetOrderComposition());
	}

	public void ForceCopositionUpdate(AssembledDefinitionNode _contents)
	{
	}

	private void OnOrderCompositionChanged(AssembledDefinitionNode _contents)
	{
		UpdateAppearance(_contents);
		if (m_rendererSceneInfo != null)
		{
			m_rendererSceneInfo.ApplySettings(true);
		}
	}

	protected virtual void OnDestroy()
	{
		if (m_iOrderDefinition != null)
		{
			m_iOrderDefinition.UnregisterOrderCompositionChangedCallback(OnOrderCompositionChanged);
		}
	}
}
