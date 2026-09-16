using Team17.Online.Multiplayer.Messaging;
using UnityEngine;
using UnityEngine.UI;

public class ClientBackpackCosmeticDecisions : ClientSynchroniserBase
{
	private BackpackCosmeticDecisions m_backpackCosmeticDecisions;

	private static readonly int m_iOpen = Animator.StringToHash("Open");

	private Animator m_animator;

	private IClientAttachment m_attachment;

	private ClientPickupItemSpawner m_itemSpawner;

	private ButtonHoverIcon m_interactHoverIcon;

	private GameObject m_contentsHoverIcon;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_backpackCosmeticDecisions = (BackpackCosmeticDecisions)synchronisedObject;
		m_animator = base.gameObject.RequestComponentRecursive<Animator>();
		m_attachment = base.gameObject.RequireInterface<IClientAttachment>();
		m_attachment.RegisterAttachChangedCallback(OnAttachmentChanged);
		m_itemSpawner = base.gameObject.RequireComponent<ClientPickupItemSpawner>();
		m_interactHoverIcon = base.gameObject.RequireComponent<ButtonHoverIcon>();
		m_interactHoverIcon.SetVisibility(true);
		m_interactHoverIcon.HoverIconController.SetFollowTransform(m_backpackCosmeticDecisions.m_hoverIconTarget);
		m_contentsHoverIcon = GameUtils.InstantiateHoverIconUIController(m_backpackCosmeticDecisions.m_contentsHoverIconPrefab, m_backpackCosmeticDecisions.m_hoverIconTarget, "HoverIconCanvas", m_backpackCosmeticDecisions.m_offset);
		m_contentsHoverIcon.SetActive(false);
		Image image = m_contentsHoverIcon.RequireChild("Icon").RequireComponent<Image>();
		GameObject itemPrefab = m_itemSpawner.GetItemPrefab();
		WorkableItem workableItem = itemPrefab.RequestComponent<WorkableItem>();
		if (workableItem != null)
		{
			GameObject nextPrefab = workableItem.GetNextPrefab();
			ISpawnableItem spawnableItem = nextPrefab.RequireInterface<ISpawnableItem>();
			image.sprite = spawnableItem.GetUIIcon();
		}
		else
		{
			ISpawnableItem spawnableItem2 = itemPrefab.RequireInterface<ISpawnableItem>();
			image.sprite = spawnableItem2.GetUIIcon();
		}
	}

	private void OnAttachmentChanged(IParentable _parentable)
	{
		if (_parentable as MonoBehaviour != null)
		{
			m_interactHoverIcon.SetVisibility(false);
			m_contentsHoverIcon.SetActive(true);
			GameUtils.TriggerAudio(GameOneShotAudioTag.DLC_05_Bag_Pickup, base.gameObject.layer);
		}
		else
		{
			m_interactHoverIcon.SetVisibility(true);
			m_contentsHoverIcon.SetActive(false);
		}
	}

	public void OnPickupItem()
	{
		m_animator.SetTrigger(m_iOpen);
		GameUtils.TriggerAudio(GameOneShotAudioTag.DLC_05_Item_Collect, base.gameObject.layer);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (m_attachment != null)
		{
			m_attachment.UnregisterAttachChangedCallback(OnAttachmentChanged);
		}
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		if (m_contentsHoverIcon != null)
		{
			m_contentsHoverIcon.SetActive(false);
		}
		if (m_interactHoverIcon != null)
		{
			m_interactHoverIcon.enabled = false;
		}
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		if (m_interactHoverIcon != null)
		{
			m_interactHoverIcon.enabled = true;
		}
	}
}
