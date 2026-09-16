using System.Collections.Generic;
using UnityEngine;

public class ServerWaterGunSpray : ServerFireExtinguishSpray, IWindSource
{
	private WaterGunSpray m_waterGunSpray;

	private List<ServerWashable> m_activeWashables = new List<ServerWashable>();

	private List<WindAccumulator> m_activeWindReceivers = new List<WindAccumulator>();

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_waterGunSpray = (WaterGunSpray)synchronisedObject;
	}

	public override void UpdateSynchronising()
	{
		base.UpdateSynchronising();
		if (IsSpraying())
		{
			List<ServerWashable> allWashable = ServerWashable.GetAllWashable();
			List<WindAccumulator> allWindReceivers = WindAccumulator.GetAllWindReceivers();
			UpdateSprayableCollection(ref m_activeWashables, allWashable, StartWashing, StopWashing);
			UpdateSprayableCollection(ref m_activeWindReceivers, allWindReceivers, StartKnockback, StopKnockback);
			return;
		}
		if (m_activeWashables.Count > 0)
		{
			ClearSprayableCollection(ref m_activeWashables, StopWashing);
		}
		if (m_activeWindReceivers.Count > 0)
		{
			ClearSprayableCollection(ref m_activeWindReceivers, StopKnockback);
		}
	}

	private void UpdateSprayableCollection<T>(ref List<T> _activeSprayables, List<T> _possibleSprayables, GenericVoid<T> _enterCallback, GenericVoid<T> _exitCallback) where T : Component
	{
		for (int i = 0; i < _possibleSprayables.Count; i++)
		{
			T val = _possibleSprayables[i];
			int num = _activeSprayables.IndexOf(val);
			bool flag = IsInSpray(val.transform);
			bool flag2 = num >= 0;
			if (!flag2 && flag)
			{
				_activeSprayables.Add(val);
				_enterCallback(_possibleSprayables[i]);
			}
			else if (flag2 && !flag)
			{
				_activeSprayables.RemoveAt(num);
				_exitCallback(val);
			}
		}
	}

	private void ClearSprayableCollection<T>(ref List<T> _activeSprayables, GenericVoid<T> _removeCallback)
	{
		if (_activeSprayables.Count > 0)
		{
			for (int i = 0; i < _activeSprayables.Count; i++)
			{
				_removeCallback(_activeSprayables[i]);
			}
			_activeSprayables.Clear();
		}
	}

	private void StartWashing(ServerWashable _washable)
	{
		_washable.StartWashing(this, m_waterGunSpray.m_washSpeed);
	}

	private void StopWashing(ServerWashable _washable)
	{
		_washable.StopWashing(this);
	}

	private void StartKnockback(WindAccumulator _windReceiver)
	{
		_windReceiver.AddWindSource(this);
	}

	private void StopKnockback(WindAccumulator _windReceiver)
	{
		_windReceiver.RemoveWindSource(this);
	}

	public Vector3 GetVelocity()
	{
		return base.transform.forward * m_waterGunSpray.m_knockbackForce;
	}
}
