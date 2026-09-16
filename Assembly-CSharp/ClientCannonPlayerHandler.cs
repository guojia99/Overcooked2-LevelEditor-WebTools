using System.Collections;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientCannonPlayerHandler : ClientSynchroniserBase, IClientCannonHandler
{
	private ClientCannon m_cannon;

	private PlayerControls m_controls;

	private PlayerIDProvider m_playerIdProvider;

	private bool m_inCannon;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_cannon = synchronisedObject.gameObject.RequireComponent<ClientCannon>();
	}

	public override void UpdateSynchronising()
	{
		if (m_controls != null && !m_inCannon && m_playerIdProvider.IsLocallyControlled())
		{
			if (!m_controls.GetDirectlyUnderPlayerControl())
			{
				m_controls.ThrowIndicator.Hide();
			}
			else
			{
				m_controls.ThrowIndicator.Show(false);
			}
		}
	}

	public void Load(GameObject _player)
	{
		m_controls = _player.RequireComponent<PlayerControls>();
		m_controls.AllowSwitchingWhenDisabled = true;
		m_controls.ThrowIndicator.Hide();
		m_playerIdProvider = _player.RequireComponent<PlayerIDProvider>();
		m_inCannon = true;
		DynamicLandscapeParenting dynamicLandscapeParenting = _player.RequestComponent<DynamicLandscapeParenting>();
		if (dynamicLandscapeParenting != null)
		{
			dynamicLandscapeParenting.enabled = false;
		}
		ClientWorldObjectSynchroniser clientWorldObjectSynchroniser = _player.RequestComponent<ClientWorldObjectSynchroniser>();
		if (clientWorldObjectSynchroniser != null)
		{
			clientWorldObjectSynchroniser.Pause();
		}
	}

	public IEnumerator ExitCannonRoutine(GameObject _player, Vector3 _exitPosition, Quaternion _exitRotation)
	{
		m_inCannon = false;
		m_controls.AllowSwitchingWhenDisabled = false;
		m_controls = null;
		m_playerIdProvider = null;
		if (_player != null)
		{
			DynamicLandscapeParenting dynamicParenting = _player.RequestComponent<DynamicLandscapeParenting>();
			if (dynamicParenting != null)
			{
				dynamicParenting.enabled = true;
			}
			yield return null;
		}
		if (_player != null)
		{
			ClientWorldObjectSynchroniser synchroniser = _player.RequireComponent<ClientWorldObjectSynchroniser>();
			while (synchroniser != null && !synchroniser.IsReadyToResume())
			{
				yield return null;
			}
			if (synchroniser != null)
			{
				synchroniser.Resume();
			}
		}
	}

	public bool CanHandle(GameObject _obj)
	{
		if (_obj == null)
		{
			return false;
		}
		return _obj.GetComponent<PlayerControls>() != null;
	}

	public void Launch(GameObject _obj)
	{
		if (_obj != null)
		{
			m_inCannon = false;
			m_controls.enabled = false;
			m_controls.Motion.SetKinematic(true);
			Collider collider = _obj.RequireComponent<Collider>();
			collider.enabled = false;
		}
	}

	public void Land(GameObject _obj)
	{
		if (_obj != null)
		{
			m_controls.enabled = true;
			if (m_controls.GetComponent<PlayerIDProvider>().IsLocallyControlled())
			{
				m_controls.Motion.SetKinematic(false);
			}
			Collider collider = _obj.RequireComponent<Collider>();
			collider.enabled = true;
			m_controls.GetComponent<ClientPlayerControlsImpl_Default>().ApplyImpact(m_controls.transform.forward.XZ() * 2f, 0.2f);
		}
	}
}
