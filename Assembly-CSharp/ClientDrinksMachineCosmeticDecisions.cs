using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientDrinksMachineCosmeticDecisions : ClientSynchroniserBase
{
	private DrinksMachineCosmeticDecisions m_drinksMachineCosmeticDecisions;

	private static readonly int[] m_iDrinks = new int[3]
	{
		Animator.StringToHash("Drink3"),
		Animator.StringToHash("Drink2"),
		Animator.StringToHash("Drink1")
	};

	private Animator m_animator;

	private Transform m_meshTransform;

	private PickupItemSwitcher m_pickupItemSwitcher;

	private ClientPickupItemSpawner m_spawner;

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_drinksMachineCosmeticDecisions = (DrinksMachineCosmeticDecisions)synchronisedObject;
		m_animator = base.gameObject.RequestComponentRecursive<Animator>();
		m_pickupItemSwitcher = base.gameObject.RequireComponent<PickupItemSwitcher>();
		m_spawner = base.gameObject.RequireComponent<ClientPickupItemSpawner>();
		OnItemSwitched();
	}

	public void OnItemSwitched()
	{
		GameObject itemPrefab = m_spawner.GetItemPrefab();
		for (int i = 0; i < m_pickupItemSwitcher.m_itemPrefabs.Length; i++)
		{
			if (m_pickupItemSwitcher.m_itemPrefabs[i] == itemPrefab)
			{
				m_animator.SetTrigger(m_iDrinks[i]);
			}
			else
			{
				m_animator.ResetTrigger(m_iDrinks[i]);
			}
		}
	}

	public void OnPickupItem()
	{
		for (int i = 0; i < m_drinksMachineCosmeticDecisions.m_particleEffects.Length; i++)
		{
			m_drinksMachineCosmeticDecisions.m_particleEffects[i].SetActive(false);
		}
		GameObject itemPrefab = m_spawner.GetItemPrefab();
		for (int j = 0; j < m_pickupItemSwitcher.m_itemPrefabs.Length; j++)
		{
			if (!(m_pickupItemSwitcher.m_itemPrefabs[j] == itemPrefab))
			{
				continue;
			}
			for (int k = 0; k < m_drinksMachineCosmeticDecisions.m_particleEffects.Length; k++)
			{
				if (k == j)
				{
					m_drinksMachineCosmeticDecisions.m_particleEffects[k].SetActive(true);
					GameUtils.TriggerAudio(GameOneShotAudioTag.DLC_08_Drinks_Machine_Dispense, base.gameObject.layer);
				}
				else
				{
					m_drinksMachineCosmeticDecisions.m_particleEffects[k].SetActive(false);
				}
			}
		}
	}
}
