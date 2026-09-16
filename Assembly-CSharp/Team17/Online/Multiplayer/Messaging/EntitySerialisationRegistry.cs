using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Team17.Online.Multiplayer.Messaging
{
	public class EntitySerialisationRegistry
	{
		public const uint kInvalidEntityID = 0u;

		public const uint kMaxEntityID = 1023u;

		public const string c_tagNetworkStatic = "NetworkStatic";

		public static bool HasUrgentOutgoingUpdates = false;

		public static Dictionary<uint, EntitySerialisationEntry> m_Entities = new Dictionary<uint, EntitySerialisationEntry>();

		public static Dictionary<GameObject, EntitySerialisationEntry> m_EntitiesByGameObject = new Dictionary<GameObject, EntitySerialisationEntry>();

		public static FastList<EntitySerialisationEntry> m_EntitiesList = new FastList<EntitySerialisationEntry>();

		private static FastList<KeyValuePair<Type, FastList<Type>>> m_ServerSerialisedTypes = new FastList<KeyValuePair<Type, FastList<Type>>>();

		private static FastList<KeyValuePair<Type, FastList<Type>>> m_ClientSerialisedTypes = new FastList<KeyValuePair<Type, FastList<Type>>>();

		private static InstancesPerGameObject m_InstancesPerObjectDefault = InstancesPerGameObject.Single;

		private static Dictionary<Type, InstancesPerGameObject> m_InstancesPerObjectForServerSynchronisedTypes = new Dictionary<Type, InstancesPerGameObject>();

		private static Dictionary<Type, InstancesPerGameObject> m_InstancesPerObjectForClientSynchronisedTypes = new Dictionary<Type, InstancesPerGameObject>();

		public static Queue<ushort> m_ServerFreeEntityIDList = new Queue<ushort>(1022);

		public static GenericVoid<EntitySerialisationEntry> OnEntryAdded = null;

		public static GenericVoid<EntitySerialisationEntry> OnEntryRemoved = null;

		private static bool s_bLinkingEntities = false;

		public static EntitySerialisationEntry GetEntry(uint uEntityID)
		{
			if (m_Entities.ContainsKey(uEntityID))
			{
				return m_Entities[uEntityID];
			}
			return null;
		}

		public static EntitySerialisationEntry GetEntry(GameObject gameObject)
		{
			if (m_EntitiesByGameObject.ContainsKey(gameObject))
			{
				return m_EntitiesByGameObject[gameObject];
			}
			return null;
		}

		public static uint GetId(GameObject gameObject)
		{
			if (m_EntitiesByGameObject.ContainsKey(gameObject))
			{
				return m_EntitiesByGameObject[gameObject].m_Header.m_uEntityID;
			}
			return 0u;
		}

		public void Clear()
		{
			m_Entities.Clear();
			m_EntitiesByGameObject.Clear();
			m_EntitiesList.Clear();
			m_ServerSerialisedTypes.Clear();
			m_ClientSerialisedTypes.Clear();
			m_ServerFreeEntityIDList.Clear();
		}

		private void SetupSynchronisedType(ref Dictionary<Type, InstancesPerGameObject> instancesPerObjectForType, ref FastList<Type> validTypes, Type syncType, InstancesPerGameObject option)
		{
			if (syncType != null)
			{
				if (instancesPerObjectForType.ContainsKey(syncType))
				{
					instancesPerObjectForType[syncType] = option;
				}
				else
				{
					instancesPerObjectForType.Add(syncType, option);
				}
				validTypes.Add(syncType);
			}
		}

		public void AddSynchronisedType(Type gameType, SynchroniserConfig config)
		{
			AddSynchronisedType(gameType, new SynchroniserConfig[1] { config });
		}

		public void AddSynchronisedType(Type gameType, SynchroniserConfig[] configs)
		{
			FastList<Type> validTypes = new FastList<Type>(configs.Length);
			FastList<Type> validTypes2 = new FastList<Type>(configs.Length);
			bool flag = ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession();
			foreach (SynchroniserConfig synchroniserConfig in configs)
			{
				if (flag)
				{
					SetupSynchronisedType(ref m_InstancesPerObjectForServerSynchronisedTypes, ref validTypes, synchroniserConfig.m_ServerSynchroniserType, synchroniserConfig.m_InstancesAllowed);
				}
				SetupSynchronisedType(ref m_InstancesPerObjectForClientSynchronisedTypes, ref validTypes2, synchroniserConfig.m_ClientSynchroniserType, synchroniserConfig.m_InstancesAllowed);
			}
			if (flag)
			{
				m_ServerSerialisedTypes.Add(new KeyValuePair<Type, FastList<Type>>(gameType, validTypes));
			}
			m_ClientSerialisedTypes.Add(new KeyValuePair<Type, FastList<Type>>(gameType, validTypes2));
		}

		public IEnumerator SetupSynchronisation(MultiplayerController multiplayerpController)
		{
			m_ServerFreeEntityIDList.Clear();
			ushort num = 1;
			while ((uint)num < 1023u)
			{
				m_ServerFreeEntityIDList.Enqueue(num);
				num++;
			}
			IEnumerator linkRoutine = LinkAllEntitiesToSynchronisationScripts();
			while (linkRoutine.MoveNext())
			{
				yield return null;
			}
			int iCount = m_EntitiesList.Count;
			for (int i = 0; i < iCount; i++)
			{
				EntitySerialisationEntry entitySerialisationEntry = m_EntitiesList._items[i];
				if (entitySerialisationEntry != null)
				{
				}
			}
		}

		public void StartSynchronisation()
		{
			int count = m_EntitiesList.Count;
			for (int i = 0; i < count; i++)
			{
				StartSynchronisingEntry(m_EntitiesList._items[i]);
			}
		}

		public void StopSynchronisation()
		{
			int count = m_EntitiesList.Count;
			for (int i = 0; i < count; i++)
			{
				StopSynchronisingEntry(m_EntitiesList._items[i]);
			}
		}

		private static IEnumerator LinkAllEntitiesToSynchronisationScripts()
		{
			s_bLinkingEntities = true;
			GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
			float yieldTime = Time.realtimeSinceStartup + 0.1f;
			for (int i = 0; i < rootObjects.Length; i++)
			{
				if (rootObjects[i] != null && rootObjects[i].CompareTag("NetworkStatic"))
				{
					continue;
				}
				for (int iServerSync = 0; iServerSync < m_ServerSerialisedTypes.Count; iServerSync++)
				{
					KeyValuePair<Type, FastList<Type>> serverSync = m_ServerSerialisedTypes._items[iServerSync];
					if (rootObjects[i] != null)
					{
						Component[] componentsInChildren = rootObjects[i].GetComponentsInChildren(serverSync.Key, true);
						for (int j = 0; j < componentsInChildren.Length; j++)
						{
							GameObject gameObject = componentsInChildren[j].gameObject;
							EntitySerialisationEntry entitySerialisationEntry = GetEntry(gameObject);
							for (int k = 0; k < serverSync.Value.Count; k++)
							{
								Type type = serverSync.Value._items[k];
								if (entitySerialisationEntry != null)
								{
									TryAddServerSynchronisationComponent(entitySerialisationEntry, type, componentsInChildren[j]);
									continue;
								}
								entitySerialisationEntry = AddEntry(gameObject);
								TryAddServerSynchronisationComponent(entitySerialisationEntry, type, componentsInChildren[j]);
							}
						}
					}
					if (Time.realtimeSinceStartup >= yieldTime)
					{
						yield return null;
						yieldTime = Time.realtimeSinceStartup + 0.1f;
					}
				}
				FastList<Type> types = null;
				for (int iClientSync = 0; iClientSync < m_ClientSerialisedTypes.Count; iClientSync++)
				{
					KeyValuePair<Type, FastList<Type>> clientSync = m_ClientSerialisedTypes._items[iClientSync];
					if (rootObjects[i] != null)
					{
						Component[] componentsInChildren2 = rootObjects[i].GetComponentsInChildren(clientSync.Key, true);
						for (int l = 0; l < componentsInChildren2.Length; l++)
						{
							GameObject gameObject2 = componentsInChildren2[l].gameObject;
							EntitySerialisationEntry entitySerialisationEntry2 = GetEntry(gameObject2);
							types = clientSync.Value;
							for (int m = 0; m < types.Count; m++)
							{
								Type type2 = types._items[m];
								if (entitySerialisationEntry2 != null)
								{
									TryAddClientSynchronisationComponent(entitySerialisationEntry2, type2, componentsInChildren2[l]);
									continue;
								}
								entitySerialisationEntry2 = AddEntry(gameObject2);
								TryAddClientSynchronisationComponent(entitySerialisationEntry2, type2, componentsInChildren2[l]);
							}
						}
					}
					if (Time.realtimeSinceStartup >= yieldTime)
					{
						yield return null;
						yieldTime = Time.realtimeSinceStartup + 0.1f;
					}
				}
			}
			s_bLinkingEntities = false;
		}

		private static void StartSynchronisingEntry(EntitySerialisationEntry entry)
		{
			FastList<ServerSynchroniser> serverSynchronisedComponents = entry.m_ServerSynchronisedComponents;
			int count = serverSynchronisedComponents.Count;
			for (int i = 0; i < count; i++)
			{
				Synchroniser synchroniser = serverSynchronisedComponents._items[i];
				synchroniser.StartSynchronising(synchroniser.GetSynchronisedComponent());
			}
			FastList<ClientSynchroniser> clientSynchronisedComponents = entry.m_ClientSynchronisedComponents;
			int count2 = clientSynchronisedComponents.Count;
			for (int j = 0; j < count2; j++)
			{
				Synchroniser synchroniser2 = clientSynchronisedComponents._items[j];
				synchroniser2.StartSynchronising(synchroniser2.GetSynchronisedComponent());
			}
		}

		public static void ServerRegisterObject(GameObject gameObject)
		{
			RegisterObject(gameObject, ServerGenerateEntityID());
		}

		public static void RegisterObject(GameObject gameObject, uint uEntityID)
		{
			if (GetEntry(gameObject) != null || GetEntry(uEntityID) != null)
			{
				return;
			}
			EntitySerialisationEntry entitySerialisationEntry = AddEntry(gameObject, uEntityID);
			for (int i = 0; i < m_ServerSerialisedTypes.Count; i++)
			{
				KeyValuePair<Type, FastList<Type>> keyValuePair = m_ServerSerialisedTypes._items[i];
				Component[] componentsInChildren = gameObject.GetComponentsInChildren(keyValuePair.Key, true);
				for (int j = 0; j < componentsInChildren.Length; j++)
				{
					for (int k = 0; k < keyValuePair.Value.Count; k++)
					{
						Type type = keyValuePair.Value._items[k];
						TryAddServerSynchronisationComponent(entitySerialisationEntry, type, componentsInChildren[j]);
					}
				}
			}
			for (int l = 0; l < m_ClientSerialisedTypes.Count; l++)
			{
				KeyValuePair<Type, FastList<Type>> keyValuePair2 = m_ClientSerialisedTypes._items[l];
				Component[] componentsInChildren2 = gameObject.GetComponentsInChildren(keyValuePair2.Key, true);
				for (int m = 0; m < componentsInChildren2.Length; m++)
				{
					for (int n = 0; n < keyValuePair2.Value.Count; n++)
					{
						Type type2 = keyValuePair2.Value._items[n];
						TryAddClientSynchronisationComponent(entitySerialisationEntry, type2, componentsInChildren2[m]);
					}
				}
			}
			if ((entitySerialisationEntry.m_ServerSynchronisedComponents != null && entitySerialisationEntry.m_ServerSynchronisedComponents.Count != 0) || (entitySerialisationEntry.m_ClientSynchronisedComponents != null && entitySerialisationEntry.m_ClientSynchronisedComponents.Count != 0))
			{
				StartSynchronisingEntry(entitySerialisationEntry);
			}
		}

		public static void UnregisterObject(uint uEntityID)
		{
			EntitySerialisationEntry entry = GetEntry(uEntityID);
			StopSynchronisingEntry(entry);
			RemoveEntry(entry);
		}

		public static void UnregisterObject(GameObject gameObject)
		{
			EntitySerialisationEntry entry = GetEntry(gameObject);
			StopSynchronisingEntry(entry);
			RemoveEntry(entry);
		}

		public static void StopSynchronisingEntry(EntitySerialisationEntry entry)
		{
			if (entry != null)
			{
				FastList<ServerSynchroniser> serverSynchronisedComponents = entry.m_ServerSynchronisedComponents;
				int count = serverSynchronisedComponents.Count;
				for (int i = 0; i < count; i++)
				{
					Synchroniser synchroniser = serverSynchronisedComponents._items[i];
					synchroniser.StopSynchronising();
				}
				FastList<ClientSynchroniser> clientSynchronisedComponents = entry.m_ClientSynchronisedComponents;
				int count2 = clientSynchronisedComponents.Count;
				for (int j = 0; j < count2; j++)
				{
					Synchroniser synchroniser2 = clientSynchronisedComponents._items[j];
					synchroniser2.StopSynchronising();
				}
			}
		}

		private static EntitySerialisationEntry AddEntry(GameObject gameObject)
		{
			return AddEntry(gameObject, ServerGenerateEntityID());
		}

		private static EntitySerialisationEntry AddEntry(GameObject gameObject, uint entityID)
		{
			EntitySerialisationEntry entitySerialisationEntry = new EntitySerialisationEntry();
			entitySerialisationEntry.m_Header = new EntityMessageHeader();
			entitySerialisationEntry.m_Header.m_uEntityID = entityID;
			entitySerialisationEntry.m_GameObject = gameObject;
			m_Entities.Add(entityID, entitySerialisationEntry);
			m_EntitiesByGameObject.Add(entitySerialisationEntry.m_GameObject, entitySerialisationEntry);
			m_EntitiesList.Add(entitySerialisationEntry);
			if (OnEntryAdded != null)
			{
				OnEntryAdded(entitySerialisationEntry);
			}
			return entitySerialisationEntry;
		}

		private static void RemoveEntry(EntitySerialisationEntry entry)
		{
			if (entry != null)
			{
				m_Entities.Remove(entry.m_Header.m_uEntityID);
				m_EntitiesByGameObject.Remove(entry.m_GameObject);
				m_EntitiesList.Remove(entry);
				m_ServerFreeEntityIDList.Enqueue((ushort)entry.m_Header.m_uEntityID);
				if (OnEntryRemoved != null)
				{
					OnEntryRemoved(entry);
				}
			}
		}

		private static void TryAddServerSynchronisationComponent(EntitySerialisationEntry entry, Type type, Component baseComponent)
		{
			if (GetInstancesPerGameObjectForServerSynchronisedTypes(type) == InstancesPerGameObject.Multiple)
			{
				AddServerSynchronisationComponent(entry, type, baseComponent);
			}
			else if (null == entry.m_GameObject.GetComponent(type))
			{
				AddServerSynchronisationComponent(entry, type, baseComponent);
			}
		}

		private static void AddServerSynchronisationComponent(EntitySerialisationEntry entry, Type type, Component baseComponent)
		{
			Component component = entry.m_GameObject.AddComponent(type);
			ServerSynchroniser serverSynchroniser = component as ServerSynchroniser;
			if (serverSynchroniser != null)
			{
				entry.m_ServerSynchronisedComponents.Add(serverSynchroniser);
				serverSynchroniser.SetSynchronisedComponent(baseComponent);
				serverSynchroniser.Initialise(entry.m_Header.m_uEntityID, (uint)(entry.m_ServerSynchronisedComponents.Count - 1));
			}
		}

		private static InstancesPerGameObject GetInstancesPerGameObjectForServerSynchronisedTypes(Type type)
		{
			if (m_InstancesPerObjectForServerSynchronisedTypes.ContainsKey(type))
			{
				return m_InstancesPerObjectForServerSynchronisedTypes[type];
			}
			return m_InstancesPerObjectDefault;
		}

		private static void TryAddClientSynchronisationComponent(EntitySerialisationEntry entry, Type type, Component baseComponent)
		{
			if (GetInstancesPerGameObjectForClientSynchronisedTypes(type) == InstancesPerGameObject.Multiple)
			{
				AddClientSynchronisationComponent(entry, type, baseComponent);
			}
			else if (null == entry.m_GameObject.GetComponent(type))
			{
				AddClientSynchronisationComponent(entry, type, baseComponent);
			}
		}

		private static void AddClientSynchronisationComponent(EntitySerialisationEntry entry, Type type, Component baseComponent)
		{
			Component component = entry.m_GameObject.AddComponent(type);
			ClientSynchroniser clientSynchroniser = component as ClientSynchroniser;
			if (clientSynchroniser != null)
			{
				entry.m_ClientSynchronisedComponents.Add(clientSynchroniser);
				clientSynchroniser.SetSynchronisedComponent(baseComponent);
				if (!ConnectionStatus.IsHost() && ConnectionStatus.IsInSession())
				{
				}
			}
		}

		private static InstancesPerGameObject GetInstancesPerGameObjectForClientSynchronisedTypes(Type type)
		{
			if (m_InstancesPerObjectForClientSynchronisedTypes.ContainsKey(type))
			{
				return m_InstancesPerObjectForClientSynchronisedTypes[type];
			}
			return m_InstancesPerObjectDefault;
		}

		private static uint ServerGenerateEntityID()
		{
			return m_ServerFreeEntityIDList.Dequeue();
		}
	}
}
