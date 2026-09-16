using System.Collections;
using System.Collections.Generic;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientDialogueController : ClientSynchroniserBase
{
	public class PendingInstance
	{
		public int DialogueID;

		public int Index;
	}

	public class DialogueInstance
	{
		public DialogueController.Dialogue Dialogue;

		public Generic<SpeechDialogueUIController, string> CreatorFunction;

		public GameObject UIObject;

		public int CurrentIndex;
	}

	private DialogueController m_dialogueController;

	private List<PendingInstance> m_pendingData = new List<PendingInstance>();

	private List<DialogueInstance> m_instances = new List<DialogueInstance>();

	private const float c_autoAdvanceTime = 10f;

	public override EntityType GetEntityType()
	{
		return EntityType.Dialogue;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_dialogueController = (DialogueController)synchronisedObject;
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		DialogueStateMessage data = (DialogueStateMessage)serialisable;
		List<DialogueInstance> list = m_instances.FindAll((DialogueInstance x) => x.Dialogue.UniqueID == data.m_dialogueID);
		if (list.Count > 0)
		{
			for (int num = 0; num < list.Count; num++)
			{
				list[num].CurrentIndex = data.m_state;
			}
			return;
		}
		PendingInstance pendingInstance = m_pendingData.Find((PendingInstance x) => x.DialogueID == data.m_dialogueID);
		if (pendingInstance != null)
		{
			pendingInstance.Index = data.m_state;
			return;
		}
		pendingInstance = new PendingInstance();
		pendingInstance.DialogueID = data.m_dialogueID;
		pendingInstance.Index = data.m_state;
		m_pendingData.Add(pendingInstance);
	}

	public IEnumerator StartDialogue(DialogueController.Dialogue _dialogue, Transform _followTarget)
	{
		DialogueInstance dialogueInstance = new DialogueInstance();
		dialogueInstance.Dialogue = _dialogue;
		dialogueInstance.CreatorFunction = (string _text) => CreateDialogue(_dialogue.DialogueUIPrefab, _text, _followTarget);
		m_instances.Add(dialogueInstance);
		return RunDialogueRoutine(dialogueInstance);
	}

	public IEnumerator StartDialogue(DialogueController.Dialogue _dialogue, Vector2 _anchor, Vector2 _pivot, float _rotation = 0f)
	{
		DialogueInstance dialogueInstance = new DialogueInstance();
		dialogueInstance.Dialogue = _dialogue;
		dialogueInstance.CreatorFunction = (string _text) => CreateDialogue(_dialogue.DialogueUIPrefab, _text, _anchor, _pivot, _rotation);
		m_instances.Add(dialogueInstance);
		return RunDialogueRoutine(dialogueInstance);
	}

	public void Shutdown(DialogueController.Dialogue _dialogue)
	{
		List<DialogueInstance> list = m_instances.FindAll((DialogueInstance x) => x.Dialogue.UniqueID == _dialogue.UniqueID);
		for (int num = 0; num < list.Count; num++)
		{
			if (list[num].Dialogue != null)
			{
				DialogueController.StopDialogueSFX(list[num].Dialogue.DialogueAudio);
			}
			if (list[num].UIObject != null)
			{
				Object.Destroy(list[num].UIObject);
				list[num].UIObject = null;
			}
		}
		m_instances.RemoveAll((DialogueInstance x) => x.Dialogue.UniqueID == _dialogue.UniqueID);
		m_pendingData.RemoveAll((PendingInstance x) => x.DialogueID == _dialogue.UniqueID);
	}

	private SpeechDialogueUIController CreateDialogue(SpeechDialogueUIController _uiPrefab, string _text, Transform _followParent)
	{
		GameObject obj = GameUtils.InstantiateHoverIconUIController(_uiPrefab.gameObject, _followParent, "HoverIconCanvas");
		SpeechDialogueUIController speechDialogueUIController = obj.RequireComponent<SpeechDialogueUIController>();
		speechDialogueUIController.Setup(_text);
		return speechDialogueUIController;
	}

	private SpeechDialogueUIController CreateDialogue(SpeechDialogueUIController _uiPrefab, string _text, Vector2 _anchor, Vector2 _pivot, float _rotation)
	{
		GameObject obj = GameUtils.InstantiateUIController(_uiPrefab.gameObject, "UICanvas");
		RectTransform rectTransform = obj.RequireComponent<RectTransform>();
		rectTransform.anchorMin = _anchor;
		rectTransform.anchorMax = _anchor;
		rectTransform.pivot = _pivot;
		rectTransform.rotation = Quaternion.Euler(0f, 0f, _rotation);
		SpeechDialogueUIController speechDialogueUIController = obj.RequireComponent<SpeechDialogueUIController>();
		speechDialogueUIController.Setup(_text);
		return speechDialogueUIController;
	}

	private IEnumerator RunDialogueRoutine(DialogueInstance _instance)
	{
		PendingInstance pending = m_pendingData.Find((PendingInstance x) => x.DialogueID == _instance.Dialogue.UniqueID);
		if (pending != null)
		{
			_instance.CurrentIndex = pending.Index;
			m_pendingData.RemoveAll((PendingInstance x) => x.DialogueID == _instance.Dialogue.UniqueID);
			if (_instance.CurrentIndex == _instance.Dialogue.DialogueScript.Length)
			{
				yield break;
			}
		}
		ILogicalButton skipDialogue = PlayerInputLookup.GetAnyButton(PlayerInputLookup.LogicalButtonID.UISelectNotStart, PadSide.Both);
		for (int i = 0; i < _instance.Dialogue.DialogueScript.Length; i++)
		{
			skipDialogue.ClaimReleaseEvent();
			string text = _instance.Dialogue.DialogueScript[_instance.CurrentIndex];
			SpeechDialogueUIController controller = _instance.CreatorFunction(text);
			_instance.UIObject = controller.gameObject;
			if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
			{
				GameObject icon = _instance.UIObject.RequireChild("Icon");
				icon.SetActive(true);
				DialogueController.StartDialogueSFX(_instance.Dialogue.DialogueAudio, base.gameObject.layer);
				IEnumerator networkTimeout = CoroutineUtils.TimerRoutine(10f, LayerMask.NameToLayer("UI"));
				while (controller.IsPrinting() && (!ShouldSkipDialogue(skipDialogue) || !_instance.UIObject.activeInHierarchy) && networkTimeout.MoveNext())
				{
					yield return null;
				}
				if (_instance.UIObject.activeInHierarchy)
				{
					controller.SkipPrinting();
				}
				DialogueController.StopDialogueSFX(_instance.Dialogue.DialogueAudio);
				while ((!ShouldSkipDialogue(skipDialogue) || !_instance.UIObject.activeInHierarchy) && networkTimeout.MoveNext())
				{
					yield return null;
				}
				m_dialogueController.OnDialogueStateChanged(_instance.Dialogue, _instance.CurrentIndex + 1);
				while (_instance.CurrentIndex == i)
				{
					yield return null;
				}
			}
			else
			{
				DialogueController.StartDialogueSFX(_instance.Dialogue.DialogueAudio, base.gameObject.layer);
				while (controller.IsPrinting() && _instance.CurrentIndex == i)
				{
					yield return null;
				}
				DialogueController.StopDialogueSFX(_instance.Dialogue.DialogueAudio);
				while (_instance.CurrentIndex == i)
				{
					yield return null;
				}
			}
			Object.Destroy(_instance.UIObject);
			_instance.UIObject = null;
		}
	}

	private bool ShouldSkipDialogue(ILogicalButton skipDialogue)
	{
		return skipDialogue.JustPressed() && (T17InGameFlow.Instance == null || !T17InGameFlow.Instance.WasPauseMenuOpen);
	}
}
