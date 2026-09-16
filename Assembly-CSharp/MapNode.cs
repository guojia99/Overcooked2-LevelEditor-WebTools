using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class MapNode : MonoBehaviour
{
	[Serializable]
	public class FlipSet
	{
		public WorldMapFlipperBase[] Tiles = new WorldMapFlipperBase[0];
	}

	[SerializeField]
	private float m_timeBetweenFlipLayers = 0.5f;

	[SerializeField]
	private MeshRenderer[] m_visibilityMesh = new MeshRenderer[0];

	private FlipSet[] m_orderedUnfoldingTiles = new FlipSet[0];

	private bool m_unfolding;

	private bool m_unfolded;

	private bool m_static;

	private VoidGeneric<FlipDirection, FlipType> m_flipCallback = delegate
	{
	};

	private VoidGeneric<FlipDirection, FlipType> m_preFlipCallback = delegate
	{
	};

	private VoidGeneric<FlipDirection, FlipType> m_postFlipCallback = delegate
	{
	};

	public bool Unfolding
	{
		get
		{
			return m_unfolding;
		}
	}

	public bool Unfolded
	{
		get
		{
			return m_unfolded;
		}
	}

	public void RegisterFlipCallback(VoidGeneric<FlipDirection, FlipType> _callback)
	{
		m_flipCallback = (VoidGeneric<FlipDirection, FlipType>)Delegate.Combine(m_flipCallback, _callback);
	}

	public void UnregisterFlipCallback(VoidGeneric<FlipDirection, FlipType> _callback)
	{
		m_flipCallback = (VoidGeneric<FlipDirection, FlipType>)Delegate.Remove(m_flipCallback, _callback);
	}

	public void RegisterPreFlipCallback(VoidGeneric<FlipDirection, FlipType> _callback)
	{
		m_preFlipCallback = (VoidGeneric<FlipDirection, FlipType>)Delegate.Combine(m_preFlipCallback, _callback);
	}

	public void UnregisterPreFlipCallback(VoidGeneric<FlipDirection, FlipType> _callback)
	{
		m_preFlipCallback = (VoidGeneric<FlipDirection, FlipType>)Delegate.Remove(m_preFlipCallback, _callback);
	}

	public void RegisterPostFlipCallback(VoidGeneric<FlipDirection, FlipType> _callback)
	{
		m_postFlipCallback = (VoidGeneric<FlipDirection, FlipType>)Delegate.Combine(m_postFlipCallback, _callback);
	}

	public void UnregisterPostFlipCallback(VoidGeneric<FlipDirection, FlipType> _callback)
	{
		m_postFlipCallback = (VoidGeneric<FlipDirection, FlipType>)Delegate.Remove(m_postFlipCallback, _callback);
	}

	protected virtual void OnEnable()
	{
		for (int i = 0; i < m_visibilityMesh.Length; i++)
		{
			m_visibilityMesh[i].gameObject.SetActive(true);
		}
	}

	protected virtual void OnDisable()
	{
		for (int i = 0; i < m_visibilityMesh.Length; i++)
		{
			m_visibilityMesh[i].gameObject.SetActive(false);
		}
	}

	protected virtual void Update()
	{
	}

	public void AddToFlipSet(int _flipSet, WorldMapFlipperBase _flipTile)
	{
		if (_flipSet < m_orderedUnfoldingTiles.Length)
		{
			ArrayUtils.PushBack(ref m_orderedUnfoldingTiles[_flipSet].Tiles, _flipTile);
		}
		else if (_flipSet >= m_orderedUnfoldingTiles.Length)
		{
			ArrayUtils.PushBack(ref m_orderedUnfoldingTiles, new FlipSet());
			AddToFlipSet(_flipSet, _flipTile);
		}
	}

	public IEnumerator UnfoldFlow()
	{
		m_unfolding = true;
		m_preFlipCallback(FlipDirection.Unfold, FlipType.Normal);
		bool flippedTile = false;
		bool flippedRamp = false;
		for (int i = 0; i < m_orderedUnfoldingTiles.Length; i++)
		{
			for (int j = 0; j < m_orderedUnfoldingTiles[i].Tiles.Length; j++)
			{
				WorldMapFlipperBase worldMapFlipperBase = m_orderedUnfoldingTiles[i].Tiles[j];
				if (!flippedTile && worldMapFlipperBase is WorldMapTileFlip)
				{
					flippedTile = true;
					GameUtils.TriggerAudio(GameOneShotAudioTag.WorldMapTiles, base.gameObject.layer);
				}
				if (!flippedRamp && worldMapFlipperBase is WorldMapRampFlip)
				{
					flippedRamp = true;
					GameUtils.TriggerAudio(GameOneShotAudioTag.WorldMapRamp, base.gameObject.layer);
				}
				worldMapFlipperBase.StartUnfoldFlow();
			}
			IEnumerator wait = CoroutineUtils.TimerRoutine(m_timeBetweenFlipLayers, base.gameObject.layer);
			while (wait.MoveNext())
			{
				yield return null;
			}
		}
		m_flipCallback(FlipDirection.Unfold, FlipType.Normal);
		for (int k = 0; k < m_orderedUnfoldingTiles.Length; k++)
		{
			for (int l = 0; l < m_orderedUnfoldingTiles[k].Tiles.Length; l++)
			{
				WorldMapFlipperBase flipperBase = m_orderedUnfoldingTiles[k].Tiles[l];
				while (!flipperBase.IsFinishedFlipping())
				{
					yield return null;
				}
			}
		}
		m_unfolded = true;
		m_unfolding = false;
		m_postFlipCallback(FlipDirection.Unfold, FlipType.Normal);
	}

	public List<ClientWorldMapInfoPopup.InfoPopupShowRequest> GetPopups()
	{
		List<ClientWorldMapInfoPopup.InfoPopupShowRequest> list = new List<ClientWorldMapInfoPopup.InfoPopupShowRequest>();
		for (int i = 0; i < m_orderedUnfoldingTiles.Length; i++)
		{
			for (int j = 0; j < m_orderedUnfoldingTiles[i].Tiles.Length; j++)
			{
				list.AddRange(m_orderedUnfoldingTiles[i].Tiles[j].GetPopups());
			}
		}
		return list;
	}

	public void InstantUnfold()
	{
		m_unfolded = true;
		m_preFlipCallback(FlipDirection.Unfold, FlipType.Instantaneous);
		for (int i = 0; i < m_orderedUnfoldingTiles.Length; i++)
		{
			for (int j = 0; j < m_orderedUnfoldingTiles[i].Tiles.Length; j++)
			{
				WorldMapFlipperBase worldMapFlipperBase = m_orderedUnfoldingTiles[i].Tiles[j];
				worldMapFlipperBase.StartInstantUnfold();
			}
		}
		m_flipCallback(FlipDirection.Unfold, FlipType.Instantaneous);
		m_postFlipCallback(FlipDirection.Unfold, FlipType.Instantaneous);
	}

	public bool IsIdle()
	{
		if (m_unfolding)
		{
			return false;
		}
		for (int i = 0; i < m_orderedUnfoldingTiles.Length; i++)
		{
			for (int j = 0; j < m_orderedUnfoldingTiles[i].Tiles.Length; j++)
			{
				WorldMapFlipperBase worldMapFlipperBase = m_orderedUnfoldingTiles[i].Tiles[j];
				if (!worldMapFlipperBase.IsFinishedFlipping())
				{
					return false;
				}
			}
		}
		return true;
	}

	public bool IsStatic()
	{
		return m_static;
	}

	public void SetAsStatic()
	{
		for (int i = 0; i < m_orderedUnfoldingTiles.Length; i++)
		{
			FlipSet flipSet = m_orderedUnfoldingTiles[i];
			for (int j = 0; j < flipSet.Tiles.Length; j++)
			{
				WorldMapFlipperBase worldMapFlipperBase = flipSet.Tiles[j];
				ITileFlipStaticHandler tileFlipStaticHandler = worldMapFlipperBase.gameObject.RequestInterfaceRecursive<ITileFlipStaticHandler>();
				if (tileFlipStaticHandler != null)
				{
					tileFlipStaticHandler.SetAsStatic();
				}
			}
		}
		m_static = true;
	}

	public void StartUp()
	{
		for (int i = 0; i < m_orderedUnfoldingTiles.Length; i++)
		{
			for (int j = 0; j < m_orderedUnfoldingTiles[i].Tiles.Length; j++)
			{
				ITileFlipStartup tileFlipStartup = m_orderedUnfoldingTiles[i].Tiles[j].gameObject.RequestInterface<ITileFlipStartup>();
				if (tileFlipStartup != null)
				{
					tileFlipStartup.StartUp();
				}
			}
		}
	}
}
