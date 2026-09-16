using System;
using UnityEngine;

[Serializable]
public abstract class LevelObjectiveBase : ScriptableObject, IObjective
{
	public abstract void Initialise();

	public abstract void CleanUp();

	public virtual bool IsObjectiveComplete()
	{
		return true;
	}
}
