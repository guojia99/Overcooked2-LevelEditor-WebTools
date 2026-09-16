using System;
using UnityEngine;

[Serializable]
public class PlatingStepData : ScriptableObject
{
	public SubTexture2D m_icon;

	public Sprite m_iconSprite;

	public GameOneShotAudioTag m_addToSound;

	[SelfAssignID(true)]
	public int m_uID;

	public override int GetHashCode()
	{
		return m_uID.GetHashCode();
	}

	public override bool Equals(object other)
	{
		return Equals(other as PlatingStepData);
	}

	public bool Equals(PlatingStepData other)
	{
		if (object.ReferenceEquals(other, null))
		{
			return false;
		}
		if (object.ReferenceEquals(other, this))
		{
			return true;
		}
		return m_uID == other.m_uID;
	}

	public static bool operator ==(PlatingStepData lhs, PlatingStepData rhs)
	{
		if (object.ReferenceEquals(lhs, null))
		{
			return object.ReferenceEquals(rhs, null);
		}
		return lhs.Equals(rhs);
	}

	public static bool operator !=(PlatingStepData lhs, PlatingStepData rhs)
	{
		return !(lhs == rhs);
	}
}
