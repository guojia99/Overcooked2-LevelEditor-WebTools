using System.Collections.Generic;
using Team17.Online;
using UnityEngine;

public abstract class KitchenLoaderManager : Manager
{
	[SerializeField]
	private OptionalFloat m_ceilingHeight;

	public static KitchenLoaderManager s_Instance;

	private MultiplayerController m_MultiplayerController;

	private bool m_bStarted;

	protected virtual void Awake()
	{
		s_Instance = this;
		if (m_ceilingHeight.HasValue)
		{
			GameObject gameObject = GameObjectUtils.CreateOnParent(null, "Ceiling");
			gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
			BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
			Transform transform = gameObject.transform;
			transform.localScale = new Vector3(50f, 1f, 50f);
			Camera main = Camera.main;
			transform.position = main.transform.position.WithY(m_ceilingHeight.Value + 0.5f);
		}
		m_MultiplayerController = GameUtils.RequestManagerInterface<MultiplayerController>();
	}

	private void Update()
	{
		if (!m_bStarted && ConnectionModeSwitcher.GetStatus().GetProgress() == eConnectionModeSwitchProgress.Complete)
		{
			m_MultiplayerController.StartKitchen();
			m_bStarted = true;
		}
	}

	private void OnDestroy()
	{
		m_MultiplayerController.StopKitchen();
	}

	public abstract void AssignChefEntities(FastList<User> users);
}
