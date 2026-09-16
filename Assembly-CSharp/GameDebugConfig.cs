using System;
using UnityEngine;

[Serializable]
public class GameDebugConfig : ScriptableObject
{
	public enum NXDemoSplitting
	{
		Default = 0,
		LockedIntoJoycons = 1
	}

	public enum KeyboardType
	{
		Actual = 0,
		DebugForPads = 1
	}

	public enum SkipPlayerJoiningConfig
	{
		ControllerEach = 0,
		TwoSidedPads = 1
	}

	public float m_timeScale = 1f;

	public bool m_immortal;

	public bool m_skipTutorial;

	public bool m_skipPlayerJoining;

	public SkipPlayerJoiningConfig m_skipJoiningConfig;

	public bool m_infiniteChopping;

	public bool m_skipCinematics;

	public bool m_supressCompetitveMode;

	public KeyboardType m_keyboardType;

	public NXDemoSplitting NXDemoSplittingMode;
}
