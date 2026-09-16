using System;
using GameModes;
using UnityEngine;

[Serializable]
public class SceneDirectoryData : ScriptableObject
{
	public enum LevelTheme
	{
		Null = -1,
		Sushi = 0,
		Balloon = 1,
		Wizard = 2,
		Space = 3,
		Rapids = 4,
		Mine = 5,
		Random = 6,
		Beach = 7,
		Resort = 8,
		Wonderland = 9,
		ChinaTown = 10,
		Campsite = 11,
		Treehouse = 12,
		Keep = 13,
		Courtyard = 14,
		Battlements = 15,
		Outside = 16,
		Inside = 17,
		Wonderland2 = 18,
		ChinaTown2 = 19,
		Summer = 20,
		ChinaTown3 = 21,
		Count = 22
	}

	public enum World
	{
		Invalid = -1,
		Tutorial = 0,
		One = 1,
		Two = 2,
		Three = 3,
		Four = 4,
		Five = 5,
		Six = 6,
		Seven = 7,
		DLC2_One = 8,
		DLC2_Two = 9,
		DLC2_Three = 10,
		DLC3_One = 11,
		DLC4_One = 12,
		DLC5_One = 13,
		DLC5_Two = 14,
		DLC5_Three = 15,
		DLC7_One = 16,
		DLC7_Two = 17,
		DLC7_Three = 18,
		DLC8_One = 19,
		DLC8_Two = 20,
		DLC8_Three = 21,
		DLC9_One = 22,
		DLC10_One = 23,
		DLC11_One = 24,
		DLC13_One = 25,
		COUNT = 26
	}

	[Serializable]
	public class SceneDirectoryEntry
	{
		public string Label;

		public LevelTheme Theme = LevelTheme.Null;

		public World World = World.Invalid;

		public int StarCost;

		public Sprite LoadScreenOverride;

		public bool UseKitchenLoadingScreen = true;

		public bool HasScoreBoundaries = true;

		public bool ActuallyAllowed = true;

		public bool IsHidden;

		public bool AvailableInLobby = true;

		public bool LevelChainEnd;

		[SerializeField]
		[Mask(typeof(Kind))]
		public int m_supportedGameModes = -1;

		[Space]
		public PerPlayerCountDirectoryEntry[] SceneVarients = new PerPlayerCountDirectoryEntry[0];

		[ArrayIndex("Scenes", "", SerializationUtils.RootType.Top)]
		public int[] PreviousEntriesToUnlock = new int[0];

		public PerPlayerCountDirectoryEntry GetSceneVarient(int _playerCount)
		{
			int num = SceneVarients.FindIndex_Predicate((PerPlayerCountDirectoryEntry x) => x.PlayerCount == _playerCount);
			if (num == -1)
			{
				num = SceneVarients.FindIndex_Predicate((PerPlayerCountDirectoryEntry x) => x.PlayerCount == -1);
			}
			return SceneVarients.TryAtIndex(num, null);
		}
	}

	[Serializable]
	public class StarBoundaries
	{
		public int m_OneStarScore;

		public int m_TwoStarScore;

		public int m_ThreeStarScore;

		public int m_FourStarScore;
	}

	[Serializable]
	public class PerPlayerCountDirectoryEntry
	{
		public int PlayerCount = 1;

		public LevelConfigBase LevelConfig;

		[SceneName]
		public string SceneName;

		public Sprite Screenshot;

		[SerializeField]
		private StarBoundaries m_PCStarBoundaries = new StarBoundaries();

		[SerializeField]
		private StarBoundaries m_PS4StarBoundaries = new StarBoundaries();

		[SerializeField]
		private StarBoundaries m_XboxStarBoundaries = new StarBoundaries();

		[SerializeField]
		private StarBoundaries m_NXStarBoundaries = new StarBoundaries();

		public int OneStarScore
		{
			get
			{
				return GetStarBoundaries().m_OneStarScore;
			}
		}

		public int TwoStarScore
		{
			get
			{
				return GetStarBoundaries().m_TwoStarScore;
			}
		}

		public int ThreeStarScore
		{
			get
			{
				return GetStarBoundaries().m_ThreeStarScore;
			}
		}

		public int FourStarScore
		{
			get
			{
				return GetStarBoundaries().m_FourStarScore;
			}
		}

		public int GetPointsForStar(int _numStars)
		{
			switch (_numStars)
			{
			case 0:
				return 0;
			case 1:
				return OneStarScore;
			case 2:
				return TwoStarScore;
			case 3:
				return ThreeStarScore;
			case 4:
				return FourStarScore;
			default:
				return -1;
			}
		}

		public int GetStarForPoints(int _points, bool _inNGPlus = false)
		{
			if (_points < OneStarScore)
			{
				return 0;
			}
			if (_points < TwoStarScore)
			{
				return 1;
			}
			if (_points < ThreeStarScore)
			{
				return 2;
			}
			if (_points < FourStarScore || !_inNGPlus)
			{
				return 3;
			}
			return 4;
		}

		private StarBoundaries GetStarBoundaries()
		{
			return m_PCStarBoundaries;
		}

		public StarBoundaries[] GetStarBoundariesAllPlatforms()
		{
			return new StarBoundaries[4] { m_PCStarBoundaries, m_PS4StarBoundaries, m_XboxStarBoundaries, m_NXStarBoundaries };
		}
	}

	public SceneDirectoryEntry[] Scenes = new SceneDirectoryEntry[0];

	public static readonly int c_bitsPerTheme = GameUtils.GetRequiredBitCount(22);
}
