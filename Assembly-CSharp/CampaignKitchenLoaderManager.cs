using System;
using System.Collections.Generic;
using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

[ExecutionDependency(typeof(PlayerInputLookup))]
public class CampaignKitchenLoaderManager : KitchenLoaderManager
{
	[SerializeField]
	private GameObject m_singleplayerCameraPrefab;

	[SerializeField]
	private OptionalBounds m_singeplayerTargetBounds;

	protected override void Awake()
	{
		base.Awake();
		int count = ClientUserSystem.m_Users.Count;
		if (count != 1)
		{
		}
	}

	public override void AssignChefEntities(FastList<User> users)
	{
		if (users.Count == 1)
		{
			uint entityID = 0u;
			uint entity2ID = 0u;
			for (int i = 0; i < PlayerIDProvider.s_AllProviders.Count; i++)
			{
				PlayerIDProvider playerIDProvider = PlayerIDProvider.s_AllProviders._items[i];
				if (!(playerIDProvider != null))
				{
					continue;
				}
				if (playerIDProvider.GetID() == PlayerInputLookup.Player.One)
				{
					EntitySerialisationEntry entry = EntitySerialisationRegistry.GetEntry(playerIDProvider.gameObject);
					if (entry != null)
					{
						entityID = entry.m_Header.m_uEntityID;
					}
				}
				else if (playerIDProvider.GetID() == PlayerInputLookup.Player.Two)
				{
					EntitySerialisationEntry entry2 = EntitySerialisationRegistry.GetEntry(playerIDProvider.gameObject);
					if (entry2 != null)
					{
						entity2ID = entry2.m_Header.m_uEntityID;
					}
				}
			}
			users._items[0].EntityID = entityID;
			users._items[0].Entity2ID = entity2ID;
		}
		else
		{
			int num = 0;
			PlayerIDProvider[] array = PlayerIDProvider.s_AllProviders.ToArray();
			Array.Sort(array, (PlayerIDProvider x, PlayerIDProvider y) => x.GetID() - y.GetID());
			int num2 = 0;
			while (num2 < users.Count && num < array.Length)
			{
				PlayerIDProvider playerIDProvider2 = array[num];
				EntitySerialisationEntry entry3 = EntitySerialisationRegistry.GetEntry(playerIDProvider2.gameObject);
				users._items[num2].EntityID = entry3.m_Header.m_uEntityID;
				num2++;
				num++;
			}
		}
	}

	private GameObject DuplicateWithPrefab(GameObject _instance, GameObject _replacementPrefab)
	{
		GameObject gameObject = _replacementPrefab.Instantiate(_instance.transform.position, _instance.transform.rotation);
		CloneHierarchyData(gameObject.transform, _instance.transform);
		return gameObject;
	}

	private void CloneHierarchyData(Transform _receiver, Transform _sample)
	{
		if (_sample.parent != null)
		{
			_receiver.SetParent(_sample.parent);
		}
		_receiver.localPosition = _sample.localPosition;
		_receiver.localRotation = _sample.localRotation;
		_receiver.localScale = _sample.localScale;
		_receiver.name = _sample.name;
	}

	private GameObject ReplaceWithPrefab(GameObject _instance, GameObject _replacementPrefab)
	{
		GameObject result = DuplicateWithPrefab(_instance, _replacementPrefab);
		UnityEngine.Object.DestroyImmediate(_instance);
		return result;
	}

	private void OnDrawGizmosSelected()
	{
		if (m_singeplayerTargetBounds.HasValue)
		{
			Bounds value = m_singeplayerTargetBounds.Value;
			Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
			Gizmos.DrawCube(value.center, value.size);
		}
	}
}
