using Team17.Online.Multiplayer.Messaging;
using UnityEngine;
using UnityEngine.UI;

namespace GameModes.Horde
{
	public class ClientHordeLockableCosmeticDecisions : ClientSynchroniserBase, IAnticipateInteractionNotifications
	{
		private HordeLockableCosmeticDecisions m_cosmeticDecisions;

		private ClientHordeLockable m_lockable;

		private GameObject m_hoverIcon;

		private HoverIconUIController m_hoverIconController;

		private bool m_locked = true;

		public ClientAnticipateInteractionHighlight m_highlight;

		public override void StartSynchronising(Component synchronisedObject)
		{
			base.StartSynchronising(synchronisedObject);
			m_cosmeticDecisions = (HordeLockableCosmeticDecisions)synchronisedObject;
			m_lockable = base.gameObject.RequireComponent<ClientHordeLockable>();
			m_lockable.RegisterOnLock(this, OnLock);
			m_lockable.RegisterOnUnlock(this, OnUnlock);
			m_hoverIcon = GameUtils.InstantiateHoverIconUIController<HoverIconUIController>(out m_hoverIconController, m_cosmeticDecisions.m_hoverIconPrefab, m_cosmeticDecisions.m_hoverIconTarget, "HoverIconCanvas", m_cosmeticDecisions.m_offset);
			Image image = m_hoverIcon.RequireChild("Icon").RequireComponent<Image>();
			image.sprite = m_cosmeticDecisions.m_hoverIcon;
			T17Text t17Text = m_hoverIcon.RequireComponentRecursive<T17Text>();
			t17Text.SetNonLocalizedText(m_lockable.UnlockCost.ToString());
			m_hoverIcon.SetActive(false);
			m_highlight = base.gameObject.RequestComponent<ClientAnticipateInteractionHighlight>();
		}

		private void OnLock(ClientHordeLockable lockable)
		{
			m_locked = true;
			m_hoverIcon.SetActive(true);
			m_cosmeticDecisions.m_animator.SetTrigger(m_cosmeticDecisions.m_lockAnimationId);
			if (m_highlight != null)
			{
				m_highlight.enabled = true;
			}
		}

		private void OnUnlock(ClientHordeLockable lockable)
		{
			m_locked = false;
			m_hoverIcon.SetActive(false);
			m_cosmeticDecisions.m_animator.SetTrigger(m_cosmeticDecisions.m_unlockAnimationId);
			if (m_highlight != null)
			{
				m_highlight.enabled = false;
			}
		}

		public void OnInteractionAnticipationStart(InteractionType type, GameObject player)
		{
			if (m_locked)
			{
				m_hoverIcon.SetActive(true);
			}
		}

		public void OnInteractionAnticipationEnded(InteractionType type, GameObject player)
		{
			if (m_locked)
			{
				m_hoverIcon.SetActive(false);
			}
		}
	}
}
