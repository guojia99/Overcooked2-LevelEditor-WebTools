using System;
using System.Collections;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameModes.Horde
{
	public class ServerHordeFlowController : ServerFlowControllerBase, IKitchenOrderHandler
	{
		private HordeFlowController m_flowController;

		private HordeLevelConfig m_levelConfig;

		private HordeFlowMessage m_message = default(HordeFlowMessage);

		private IEnumerator m_runWaves;

		private int m_waveIndex;

		private double m_waveTime;

		private PlateReturnController m_plateReturnController;

		private ServerHordeTarget[] m_targets;

		private ServerHordeEnemy[] m_enemies;

		private RecipeList.Entry[] m_entries;

		private TeamScoreStats m_score = default(TeamScoreStats);

		private int m_flowLayerId = -1;

		private GenericVoid<int> m_onMoneyChanged;

		private List<int> m_freeTargets = new List<int>(8);

		private bool m_levelRestartRequested;

		public int MaxHealth { get; private set; }

		public int Health
		{
			get
			{
				return m_score.TotalHealth;
			}
		}

		public int Money
		{
			get
			{
				return m_score.GetTotalMoney();
			}
		}

		public void RegisterOnMoneyChanged(object handle, GenericVoid<int> onMoneyChanged)
		{
			m_onMoneyChanged = (GenericVoid<int>)Delegate.Combine(m_onMoneyChanged, onMoneyChanged);
		}

		public void UnregisterOnMoneyChanged(object handle, GenericVoid<int> onMoneyChanged)
		{
			m_onMoneyChanged = (GenericVoid<int>)Delegate.Remove(m_onMoneyChanged, onMoneyChanged);
		}

		public override EntityType GetEntityType()
		{
			return EntityType.HordeFlowController;
		}

		public override void StartSynchronising(Component synchronisedObject)
		{
			base.StartSynchronising(synchronisedObject);
			m_flowController = (HordeFlowController)synchronisedObject;
			m_levelConfig = GameUtils.GetLevelConfig() as HordeLevelConfig;
			m_flowLayerId = LayerMask.NameToLayer("Default");
			MaxHealth = m_levelConfig.m_health;
			m_score.TotalHealth = MaxHealth;
			PlateReturnController.PlateReturnControllerConfig _plateReturnControllerDesc = new PlateReturnController.PlateReturnControllerConfig
			{
				m_plateReturnTime = m_levelConfig.m_plateReturnTime
			};
			m_plateReturnController = new PlateReturnController(ref _plateReturnControllerDesc);
			m_plateReturnController.Init();
			List<GameObject> list = new List<GameObject>();
			SceneManager.GetActiveScene().GetRootGameObjects(list);
			List<ServerHordeTarget> list2 = new List<ServerHordeTarget>(8);
			for (int i = 0; i < list.Count; i++)
			{
				list2.AddRange(list[i].RequestComponentsRecursive<ServerHordeTarget>());
			}
			list2.Sort(default(ServerHordeTargetComparer));
			m_targets = list2.ToArray();
			m_enemies = new ServerHordeEnemy[m_targets.Length];
			m_entries = new RecipeList.Entry[m_targets.Length];
			for (int j = 0; j < m_levelConfig.m_waves.Count; j++)
			{
				HordeWaveData hordeWaveData = m_levelConfig.m_waves[j];
				for (int k = 0; k < hordeWaveData.m_spawns.Count; k++)
				{
					HordeSpawnData hordeSpawnData = hordeWaveData.m_spawns[k];
					NetworkUtils.RegisterSpawnablePrefab(base.gameObject, hordeSpawnData.m_prefab);
				}
			}
			m_runWaves = RunWaves(m_levelConfig.m_waves, m_flowController.m_waveNumberUIDelay);
		}

		public void Damage(int kitchenDamage)
		{
			m_score.TotalHealth = Mathf.Max(m_score.TotalHealth - kitchenDamage, 0);
			HordeFlowMessage.ScoreOnly(ref m_message, m_score);
			SendServerEvent(m_message);
		}

		public bool SpendMoney(int amount)
		{
			int totalMoney = m_score.GetTotalMoney();
			if (totalMoney >= amount)
			{
				m_score.TotalMoneySpent += amount;
				HordeFlowMessage.ScoreOnly(ref m_message, m_score);
				SendServerEvent(m_message);
				m_onMoneyChanged(m_score.GetTotalMoney());
				return true;
			}
			return false;
		}

		protected override bool HasFinished()
		{
			return m_runWaves == null || base.HasFinished();
		}

		protected override void OnUpdateInRound()
		{
			base.OnUpdateInRound();
			m_waveTime += TimeManager.GetDeltaTime(m_flowLayerId);
			if (m_runWaves == null)
			{
				return;
			}
			RemoveDead();
			if (m_score.TotalHealth > 0 && m_runWaves.MoveNext())
			{
				if (m_plateReturnController != null)
				{
					m_plateReturnController.Update();
				}
			}
			else
			{
				RemoveDead();
				m_runWaves = null;
			}
		}

		public void RemoveDead()
		{
			for (int i = 0; i < m_enemies.Length; i++)
			{
				if (m_enemies[i] != null && !m_enemies[i].IsAlive)
				{
					NetworkUtils.DestroyObject(m_enemies[i].gameObject);
					m_enemies[i] = null;
				}
			}
		}

		public bool AnyAlive()
		{
			bool flag = false;
			for (int i = 0; i < m_enemies.Length; i++)
			{
				flag |= m_enemies[i] != null && m_enemies[i].IsAlive;
			}
			return flag;
		}

		public int NextTarget()
		{
			m_freeTargets.Clear();
			for (int i = 0; i < m_enemies.Length; i++)
			{
				if (m_enemies[i] == null || !m_enemies[i].IsAlive)
				{
					m_freeTargets.Add(i);
				}
			}
			return (m_freeTargets.Count <= 0) ? (-1) : m_freeTargets[UnityEngine.Random.Range(0, m_freeTargets.Count)];
		}

		public int NextSpawn(List<HordeSpawnData> spawns, double waveTime)
		{
			for (int i = 0; i < spawns.Count; i++)
			{
				if (spawns[i].CanSpawn(waveTime))
				{
					return i;
				}
			}
			return -1;
		}

		private IEnumerator RunWaves(HordeWavesData waves, float waveNumberUIDelay)
		{
			int layerId = LayerMask.NameToLayer("Default");
			while (m_waveIndex < waves.Count)
			{
				RemoveDead();
				HordeWaveData wave = waves[m_waveIndex];
				m_waveTime = 0.0;
				HordeFlowMessage.BeginWave(ref m_message, m_waveIndex, m_score);
				SendServerEvent(m_message);
				IEnumerator timer = CoroutineUtils.TimerRoutine(waveNumberUIDelay, m_flowLayerId);
				while (timer != null && timer.MoveNext())
				{
					yield return null;
				}
				List<HordeSpawnData> spawns = new List<HordeSpawnData>(wave.m_spawns.Count);
				spawns.AddRange(wave.m_spawns);
				while (spawns.Count > 0)
				{
					int targetIndex = -1;
					while (true)
					{
						int num;
						targetIndex = (num = NextTarget());
						if (num != -1)
						{
							break;
						}
						yield return null;
					}
					int spawnIndex = -1;
					while (true)
					{
						int num;
						spawnIndex = (num = NextSpawn(spawns, m_waveTime));
						if (num != -1)
						{
							break;
						}
						yield return null;
					}
					HordeSpawnData spawn = spawns[spawnIndex];
					spawns.RemoveAt(spawnIndex);
					ServerHordeTarget target = m_targets[targetIndex];
					Quaternion dir;
					Vector3 position = target.GenerateSpawnPosition(out dir);
					GameObject obj = NetworkUtils.ServerSpawnPrefab(target.gameObject, spawn.m_prefab, position, dir);
					ServerHordeEnemy serverHordeEnemy = obj.RequireComponent<ServerHordeEnemy>();
					serverHordeEnemy.Setup(this, target);
					HordeFlowMessage.Spawn(ref m_message, targetIndex, serverHordeEnemy.gameObject);
					SendServerEvent(m_message);
					m_enemies[targetIndex] = serverHordeEnemy;
					RecipeList.Entry randomElement = wave.m_recipes.m_recipes.GetRandomElement();
					HordeFlowMessage.EntryAdded(ref m_message, targetIndex, randomElement);
					SendServerEvent(m_message);
					m_entries[targetIndex] = randomElement;
				}
				while (AnyAlive())
				{
					yield return null;
				}
				HordeFlowMessage.EndWave(ref m_message, m_waveIndex, m_score);
				SendServerEvent(m_message);
				IEnumerator timer2 = CoroutineUtils.TimerRoutine(wave.m_intervalSeconds, m_flowLayerId);
				while (timer2 != null && timer2.MoveNext())
				{
					yield return null;
				}
				m_waveIndex++;
			}
			yield return null;
		}

		public void OnLevelRestartRequested()
		{
			m_levelRestartRequested = true;
		}

		protected override string GetNextScene(out GameState o_loadState, out GameState o_loadEndState, out bool o_useLoadingScreen)
		{
			o_loadState = GameState.NotSet;
			o_loadEndState = GameState.NotSet;
			o_useLoadingScreen = true;
			if (m_levelRestartRequested)
			{
				o_loadState = GameState.LoadKitchen;
				o_loadEndState = GameState.RunLevelIntro;
				o_useLoadingScreen = true;
				return GameUtils.GetGameSession().LevelSettings.SceneDirectoryVarientEntry.SceneName;
			}
			if (ServerGameSetup.Mode == GameMode.Campaign)
			{
				o_loadState = GameState.CampaignMap;
				o_loadEndState = GameState.RunMapUnfoldRoutine;
				return GameUtils.GetGameSession().TypeSettings.WorldMapScene;
			}
			if (ServerGameSetup.Mode == GameMode.Party)
			{
				o_loadState = GameState.PartyLobby;
				o_loadEndState = GameState.NotSet;
				return GameUtils.GetGameSession().TypeSettings.WorldMapScene;
			}
			return string.Empty;
		}

		public void FoodDelivered(AssembledDefinitionNode definition, PlatingStepData plateType, ServerPlateStation station)
		{
			m_plateReturnController.FoodDelivered(definition, plateType, station);
			int num = Array.FindIndex(m_targets, (ServerHordeTarget x) => x.gameObject == station.gameObject);
			ServerHordeTarget serverHordeTarget = m_targets[num];
			ServerHordeEnemy serverHordeEnemy = m_enemies[num];
			RecipeList.Entry entry = m_entries[num];
			if (serverHordeEnemy != null && entry != null && AssembledDefinitionNode.Matching(definition, entry.m_order))
			{
				bool flag = serverHordeEnemy.Feed(entry);
				if (flag)
				{
					m_score.TotalEnemiesDefeated++;
				}
				m_score.TotalMoneyEarned += m_levelConfig.m_recipeMoney.Get(entry.m_order);
				HordeFlowMessage.SuccessfulDelivery(ref m_message, num, m_score);
				SendServerEvent(m_message);
				m_onMoneyChanged(m_score.GetTotalMoney());
				if (!flag)
				{
					m_entries[num] = m_levelConfig.m_waves[m_waveIndex].m_recipes.m_recipes.GetRandomElement();
					HordeFlowMessage.EntryAdded(ref m_message, num, m_entries[num]);
					SendServerEvent(m_message);
				}
				else
				{
					m_entries[num] = null;
				}
			}
			else
			{
				HordeFlowMessage.IncorrectDelivery(ref m_message, num, m_score);
				SendServerEvent(m_message);
			}
		}
	}
}
