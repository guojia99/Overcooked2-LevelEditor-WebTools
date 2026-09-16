using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientTriggerColourCycle : ClientSynchroniserBase
{
	private TriggerColourCycle m_triggerColourCycle;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_triggerColourCycle = (TriggerColourCycle)synchronisedObject;
		SetMaterial(0);
	}

	public override EntityType GetEntityType()
	{
		return EntityType.TriggerColourCycle;
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		int colourIndex = ((TriggerColourCycleMessage)serialisable).m_colourIndex;
		SetMaterial(colourIndex);
		SetGlowEffect(colourIndex);
	}

	private void SetGlowEffect(int index)
	{
		for (int i = 0; i < m_triggerColourCycle.m_glowEffects.Length; i++)
		{
			if (i == index)
			{
				m_triggerColourCycle.m_glowEffects[i].gameObject.SetActive(true);
			}
			else
			{
				m_triggerColourCycle.m_glowEffects[i].gameObject.SetActive(false);
			}
		}
	}

	private void SetMaterial(int index)
	{
		m_triggerColourCycle.m_renderer.material = m_triggerColourCycle.m_materials[index];
	}
}
