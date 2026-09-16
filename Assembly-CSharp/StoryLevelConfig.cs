using UnityEngine;

public class StoryLevelConfig : LevelConfigBase
{
	[Header("Story Text")]
	public bool AutoStartDialogue;

	public string[] OnionScript = new string[1] { "<Placeholder Dialog>" };

	public string[] OnionReturnScript = new string[1] { "<Placeholder Dialog>" };
}
