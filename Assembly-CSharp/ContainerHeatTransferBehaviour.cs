using UnityEngine;

public class ContainerHeatTransferBehaviour : MonoBehaviour, IHeatTransferBehaviour
{
	private IIngredientContents m_ingredientContainer;

	private void EnsureContainer()
	{
		if (m_ingredientContainer == null)
		{
			m_ingredientContainer = base.gameObject.RequestInterface<IIngredientContents>();
		}
	}

	public bool CanTransferToContainer(IHeatContainer _container)
	{
		EnsureContainer();
		int contentsCount = m_ingredientContainer.GetContentsCount();
		for (int i = 0; i < contentsCount; i++)
		{
			AssembledDefinitionNode contentsElement = m_ingredientContainer.GetContentsElement(i);
			if (contentsElement is ItemAssembledNode)
			{
				ItemAssembledNode itemAssembledNode = contentsElement as ItemAssembledNode;
				if (itemAssembledNode.m_itemOrderNode.m_heatValue > 0f)
				{
					return true;
				}
			}
		}
		return false;
	}

	public void TransferToContainer(ICarrierPlacement _carrier, IHeatContainer _container)
	{
		EnsureContainer();
		float num = 0f;
		int contentsCount = m_ingredientContainer.GetContentsCount();
		for (int num2 = contentsCount - 1; num2 >= 0; num2--)
		{
			AssembledDefinitionNode contentsElement = m_ingredientContainer.GetContentsElement(num2);
			if (contentsElement is ItemAssembledNode)
			{
				ItemAssembledNode itemAssembledNode = contentsElement as ItemAssembledNode;
				num += itemAssembledNode.m_itemOrderNode.m_heatValue;
			}
		}
		if (num > 0f)
		{
			_container.IncreaseHeat(num);
			m_ingredientContainer.Empty();
		}
	}
}
