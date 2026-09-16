using System.Collections;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerPlayerSlipBehaviour : ServerSynchroniserBase
{
	private PlayerSlipBehaviour m_slipBehaviour;

	private PlayerSlipMessage m_data = new PlayerSlipMessage();

	private PlayerControls m_playerControls;

	public override EntityType GetEntityType()
	{
		return EntityType.PlayerSlip;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_slipBehaviour = (PlayerSlipBehaviour)synchronisedObject;
		m_playerControls = base.gameObject.RequireComponent<PlayerControls>();
	}

	public void Slip(ServerSlipCollider _surface)
	{
		StartCoroutine(SlipCoroutine(_surface));
	}

	private IEnumerator SlipCoroutine(ServerSlipCollider _surface)
	{
		m_data.Initialise(PlayerSlipMessage.MsgType.Slip);
		SendServerEvent(m_data);
		DisablePlayer(m_playerControls);
		IEnumerator waitRoutine = CoroutineUtils.TimerRoutine(m_slipBehaviour.m_fallTime, base.gameObject.layer);
		while (waitRoutine.MoveNext())
		{
			yield return null;
		}
		waitRoutine = CoroutineUtils.TimerRoutine(m_slipBehaviour.m_downTime, base.gameObject.layer);
		while (waitRoutine.MoveNext())
		{
			yield return null;
		}
		m_data.Initialise(PlayerSlipMessage.MsgType.Stand);
		SendServerEvent(m_data);
		waitRoutine = CoroutineUtils.TimerRoutine(m_slipBehaviour.m_standTime, base.gameObject.layer);
		while (waitRoutine.MoveNext())
		{
			yield return null;
		}
		ReenablePlayer(m_playerControls);
		m_data.Initialise(PlayerSlipMessage.MsgType.Finished);
		SendServerEvent(m_data);
	}

	private void DisablePlayer(PlayerControls _controls)
	{
		Rigidbody rigidbody = _controls.gameObject.RequireComponent<Rigidbody>();
		rigidbody.velocity = Vector3.zero;
		rigidbody.isKinematic = true;
		Collider collider = _controls.gameObject.RequireComponent<Collider>();
		collider.enabled = false;
		m_playerControls.enabled = false;
	}

	private void ReenablePlayer(PlayerControls _controls)
	{
		Rigidbody rigidbody = _controls.gameObject.RequireComponent<Rigidbody>();
		rigidbody.isKinematic = false;
		Collider collider = _controls.gameObject.RequireComponent<Collider>();
		collider.enabled = true;
		m_playerControls.enabled = true;
	}
}
