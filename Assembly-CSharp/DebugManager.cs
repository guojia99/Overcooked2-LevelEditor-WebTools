using System.Collections.Generic;
using UnityEngine;

public class DebugManager : MonoBehaviour
{
	[SerializeField]
	private BaseMenuBehaviour m_baseMenu;

	private ILogicalButton m_button;

	private static DebugManager s_Instance;

	private Dictionary<string, bool> m_debugOptions = new Dictionary<string, bool>();

	public static DebugManager Instance
	{
		get
		{
			return s_Instance;
		}
	}

	public void Awake()
	{
		if (s_Instance != null)
		{
			Object.Destroy(this);
			return;
		}
		s_Instance = this;
		AddOption("New Gravity", true);
		AddOption("Unlock all levels", false);
		AddOption("Unlock All DLC Packs", false);
		AddOption("Chef VS Chef Prediction", true);
		AddOption("Raise all ramps", false);
		AddOption("Unlock all chefs", false);
		AddOption("Deliver Current Recipe", false);
		AddOption("Skip Level 4 star", false);
		AddOption("Skip Level 3 star", false);
		AddOption("Skip Level 2 star", false);
		AddOption("Skip Level 1 star", false);
		AddOption("Skip Level", false);
		AddOption("Freeze time", false);
		AddOption("Fast NetworkChefs", true);
		AddOption("Auto Load Levels", false);
		AddOption("On Screen Debug Text", false);
		AddOption("Toggle UI", true);
		AddOption("Reset network stats", false);
		AddOption("Corrupt All Saves", false);
		AddOption("Corrupt Save 0", false);
		AddOption("Corrupt Save 1", false);
		AddOption("Corrupt Save 2", false);
		AddOption("Corrupt Meta Save", false);
		AddOption("Fake No Space", false);
		AddOption("Delete All Saves", false);
	}

	private void Start()
	{
		m_button = PlayerInputLookup.GetButton(PlayerInputLookup.LogicalButtonID.DebugMenu, PlayerInputLookup.Player.One);
	}

	private void OnDestroy()
	{
		if (s_Instance != null)
		{
			Object.Destroy(s_Instance);
			s_Instance = null;
		}
	}

	private void AddOption(string optionName, bool defaultValue)
	{
		m_debugOptions.Add(optionName, defaultValue);
	}

	public bool GetOption(string optionName)
	{
		bool value = false;
		if (m_debugOptions.TryGetValue(optionName, out value))
		{
			return value;
		}
		return value;
	}

	public Dictionary<string, bool> GetOptions()
	{
		return m_debugOptions;
	}
}
