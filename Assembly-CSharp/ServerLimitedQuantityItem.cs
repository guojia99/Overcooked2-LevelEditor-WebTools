using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerLimitedQuantityItem : ServerSynchroniserBase
{
	private LimitedQuantityItem m_baseObject;

	private ServerLimitedQuantityItemManager m_Manager;

	private float m_fLastActivity;

	private List<Generic<bool>> m_InvincibilityConditions = new List<Generic<bool>>();

	private List<Generic<float>> m_ScoreModifiers = new List<Generic<float>>();

	public override void StartSynchronising(Component synchronisedObject)
	{
		m_baseObject = (LimitedQuantityItem)synchronisedObject;
		LimitedQuantityItemManager limitedQuantityItemManager = GameUtils.RequireManager<LimitedQuantityItemManager>();
		m_Manager = limitedQuantityItemManager.GetComponent<ServerLimitedQuantityItemManager>();
		m_Manager.AddItemToList(this);
		base.StartSynchronising(synchronisedObject);
	}

	public bool IsInvincible()
	{
		return m_InvincibilityConditions.CallForResult(true);
	}

	public void AddInvincibilityCondition(Generic<bool> isInvincible)
	{
		m_InvincibilityConditions.Add(isInvincible);
	}

	public void RemoveInvincibilityCondition(Generic<bool> isInvincible)
	{
		m_InvincibilityConditions.Remove(isInvincible);
	}

	public void RegisterImpendingDestructionNotification(ImpendingDestructionCallback func)
	{
		m_baseObject.RegisterImpendingDestructionNotification(func);
	}

	public void UnregisterImpendingDestructionNotification(ImpendingDestructionCallback func)
	{
		m_baseObject.UnregisterImpendingDestructionNotification(func);
	}

	public void AddDestructionScoreModifier(Generic<float> modifier)
	{
		m_ScoreModifiers.Add(modifier);
	}

	public void RemoveDestructionScoreModifier(Generic<float> modifier)
	{
		m_ScoreModifiers.Remove(modifier);
	}

	public float GetDestructionScore()
	{
		float num = ClientTime.Time() - m_fLastActivity;
		float num2 = 0f;
		for (int i = 0; i < m_ScoreModifiers.Count; i++)
		{
			num2 += m_ScoreModifiers[i]();
		}
		return num + num2;
	}

	public void Start()
	{
		Touch();
	}

	public override void OnDestroy()
	{
		m_InvincibilityConditions = null;
		if (m_Manager != null)
		{
			m_Manager.RemoveItemFromList(this);
		}
		base.OnDestroy();
	}

	public void Touch()
	{
		m_fLastActivity = ClientTime.Time();
	}
}
