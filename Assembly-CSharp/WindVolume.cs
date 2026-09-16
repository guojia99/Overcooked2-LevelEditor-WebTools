using Team17.Online;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class WindVolume : MonoBehaviour, IWindSource
{
	[SerializeField]
	private LayerMask m_windFilter = -1;

	[SerializeField]
	private float m_windSpeed;

	private bool m_isWindy;

	private Bounds m_volumeBounds;

	public Vector3 GetVelocity()
	{
		return (!base.enabled) ? Vector3.zero : (m_windSpeed * base.transform.right);
	}

	public void ObjectAdded(GameObject _gameObject)
	{
		if ((m_windFilter.value & (1 << _gameObject.layer)) != 0)
		{
			IWindReceiver windReceiver = _gameObject.RequestInterface<IWindReceiver>();
			if (windReceiver != null)
			{
				windReceiver.AddWindSource(this);
			}
		}
	}

	public void ObjectRemoved(GameObject _gameObject)
	{
		if ((m_windFilter.value & (1 << _gameObject.layer)) != 0)
		{
			IWindReceiver windReceiver = _gameObject.RequestInterface<IWindReceiver>();
			if (windReceiver != null)
			{
				windReceiver.RemoveWindSource(this);
			}
		}
	}

	private void OnCollisionEnter(Collision collision)
	{
		ObjectAdded(collision.gameObject);
	}

	private void OnCollisionStay(Collision collision)
	{
		ObjectAdded(collision.collider.gameObject);
	}

	private void OnTriggerEnter(Collider collider)
	{
		ObjectAdded(collider.gameObject);
	}

	private void OnTriggerStay(Collider collider)
	{
		ObjectAdded(collider.gameObject);
	}

	private void OnCollisionExit(Collision collision)
	{
		ObjectRemoved(collision.gameObject);
	}

	private void OnTriggerExit(Collider collider)
	{
		ObjectRemoved(collider.gameObject);
	}

	private void Awake()
	{
		Mailbox.Client.RegisterForMessageType(MessageType.GameState, OnGameStateChanged);
		BoxCollider boxCollider = base.gameObject.RequireComponentRecursive<BoxCollider>();
		m_volumeBounds = boxCollider.bounds;
	}

	private void OnEnable()
	{
		AddInitialObjects();
	}

	private void Update()
	{
		if (!Mathf.Approximately(m_windSpeed, 0f) && !m_isWindy)
		{
			GameUtils.TriggerAudio(GameOneShotAudioTag.WindGust, base.gameObject.layer);
			m_isWindy = true;
		}
		else if (Mathf.Approximately(m_windSpeed, 0f))
		{
			m_isWindy = false;
		}
	}

	private void OnDisable()
	{
		Collider[] array = Physics.OverlapBox(m_volumeBounds.center, m_volumeBounds.extents, base.transform.rotation);
		for (int i = 0; i < array.Length; i++)
		{
			OnTriggerExit(array[i]);
		}
		m_isWindy = false;
	}

	private void OnGameStateChanged(IOnlineMultiplayerSessionUserId sessionUserId, Serialisable message)
	{
		GameStateMessage gameStateMessage = (GameStateMessage)message;
		if (gameStateMessage.m_State == GameState.StartEntities)
		{
			AddInitialObjects();
		}
	}

	protected virtual void OnDestroy()
	{
		Mailbox.Client.UnregisterForMessageType(MessageType.GameState, OnGameStateChanged);
	}

	private void AddInitialObjects()
	{
		BoxCollider boxCollider = base.gameObject.RequireComponent<BoxCollider>();
		Collider[] array = Physics.OverlapBox(boxCollider.bounds.center, boxCollider.bounds.extents, base.transform.rotation);
		for (int i = 0; i < array.Length; i++)
		{
			OnTriggerEnter(array[i]);
		}
	}
}
