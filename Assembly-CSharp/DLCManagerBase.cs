using System;
using System.Collections.Generic;
using UnityEngine;

public class DLCManagerBase : Manager
{
	public class DLCItem
	{
		public string name;

		public string description;

		public string productId;

		public string path = string.Empty;

		public string bundlePath;

		public AssetBundle bundle;

		public void Save()
		{
		}
	}

	protected static readonly int c_dlcCount = 9;

	public const int c_MaxBitsPerDLCID = 4;

	public const int c_NoDLCID = -1;

	public static GenericVoid DLCUpdatedEvent;

	protected PlayerManager m_playerManager;

	[SerializeField]
	private List<DLCFrontendData> m_allDlc = new List<DLCFrontendData>();

	protected List<DLCItem> m_DLCItems = new List<DLCItem>();

	public static int SupportedDLCLimit
	{
		get
		{
			return c_dlcCount;
		}
	}

	public List<DLCFrontendData> AllDlc
	{
		get
		{
			return m_allDlc;
		}
	}

	public virtual bool? HasDLC(int _pack)
	{
		throw new Exception("Deprecated code");
	}

	public GameSession GetDLCSessionPrefab(int _dlcPack)
	{
		throw new Exception("Deprecated code");
	}

	private void Awake()
	{
		Cleanup();
	}

	private void Start()
	{
		Initialise();
		RefreshDLC();
	}

	protected virtual void Initialise()
	{
		m_playerManager = GameUtils.RequireManager<PlayerManager>();
	}

	protected virtual void Cleanup()
	{
		m_DLCItems.Clear();
		DLCUpdatedEvent = null;
	}

	public virtual bool ShowDLCStorePage(DLCFrontendData data)
	{
		return false;
	}

	public virtual void RefreshDLC()
	{
	}

	public bool IsDLCAvailable(DLCFrontendData data)
	{
		if (!data.IsAvailableOnThisPlatform())
		{
			return false;
		}
		if (data.m_IsFreeDLC)
		{
			return true;
		}
		DLCItem dLCItem = FindDLCItem(data.productId);
		return dLCItem != null;
	}

	protected DLCItem FindDLCItem(string productId)
	{
		for (int i = 0; i < m_DLCItems.Count; i++)
		{
			if (string.Equals(m_DLCItems[i].productId, productId, StringComparison.OrdinalIgnoreCase))
			{
				return m_DLCItems[i];
			}
		}
		return null;
	}

	protected void AddDLCItem(DLCItem item)
	{
		if (item == null)
		{
			throw new Exception("Invalid DLC item");
		}
		if (FindDLCItem(item.productId) == null)
		{
			m_DLCItems.Add(item);
		}
	}
}
