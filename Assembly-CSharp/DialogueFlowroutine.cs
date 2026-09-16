using System;
using System.Collections;
using UnityEngine;

[Serializable]
public class DialogueFlowroutine
{
	[AssignResource("DialogueUI", Editorbility.Editable)]
	public SpeechDialogueUIController DialogueUIPrefab;

	public bool ReceivesInput = true;

	public DialogueController.DialogueAudio DialogueAudio;

	public string[] DialogueScript;

	private Generic<SpeechDialogueUIController, string> m_creatorFunction;

	private GameObject m_dialogueObject;

	private bool m_moveToNextDialog;

	private bool m_ignoreInput;

	public void OnValidate()
	{
		if (!ReceivesInput)
		{
			Array.Resize(ref DialogueScript, 1);
		}
	}

	public void OnEnable()
	{
		if (m_dialogueObject != null)
		{
			m_dialogueObject.SetActive(true);
		}
	}

	public void OnDisable()
	{
		if (m_dialogueObject != null)
		{
			m_dialogueObject.SetActive(false);
		}
	}

	public void Setup(Transform _followParent)
	{
		m_creatorFunction = (string _text) => CreateDialogue(_text, _followParent);
	}

	public void Setup(Vector2 _anchor, Vector2 _pivot)
	{
		m_creatorFunction = (string _text) => CreateDialogue(_text, _anchor, _pivot);
	}

	private SpeechDialogueUIController CreateDialogue(string _text, Transform _followParent)
	{
		GameObject obj = GameUtils.InstantiateHoverIconUIController(DialogueUIPrefab.gameObject, _followParent, "HoverIconCanvas");
		SpeechDialogueUIController speechDialogueUIController = obj.RequireComponent<SpeechDialogueUIController>();
		speechDialogueUIController.Setup(_text);
		return speechDialogueUIController;
	}

	private SpeechDialogueUIController CreateDialogue(string _text, Vector2 _anchor, Vector2 _pivot)
	{
		GameObject obj = GameUtils.InstantiateUIController(DialogueUIPrefab.gameObject, "UICanvas");
		RectTransform rectTransform = obj.RequireComponent<RectTransform>();
		rectTransform.anchorMin = _anchor;
		rectTransform.anchorMax = _anchor;
		rectTransform.pivot = _pivot;
		SpeechDialogueUIController speechDialogueUIController = obj.RequireComponent<SpeechDialogueUIController>();
		speechDialogueUIController.Setup(_text);
		return speechDialogueUIController;
	}

	public IEnumerator Run()
	{
		ILogicalButton skipDialogue = PlayerInputLookup.GetAnyButton(PlayerInputLookup.LogicalButtonID.UISelectNotStart, PadSide.Both);
		for (int i = 0; i < DialogueScript.Length; i++)
		{
			SpeechDialogueUIController controller = m_creatorFunction(DialogueScript[i]);
			m_dialogueObject = controller.gameObject;
			if (ReceivesInput)
			{
				skipDialogue.ClaimPressEvent();
				if (!m_ignoreInput)
				{
					GameObject gameObject = m_dialogueObject.RequireChild("Icon");
					gameObject.SetActive(true);
				}
				DialogueController.StartDialogueSFX(DialogueAudio, LayerMask.NameToLayer("Default"));
				while ((m_ignoreInput || ((!skipDialogue.JustPressed() || !m_dialogueObject.activeInHierarchy) && controller.IsPrinting())) && !m_moveToNextDialog)
				{
					yield return null;
				}
				if (m_dialogueObject.activeInHierarchy)
				{
					controller.SkipPrinting();
				}
				DialogueController.StopDialogueSFX(DialogueAudio);
				while ((!skipDialogue.JustPressed() || !m_dialogueObject.activeInHierarchy) && !m_moveToNextDialog)
				{
					yield return null;
				}
				m_moveToNextDialog = false;
				UnityEngine.Object.Destroy(m_dialogueObject);
				m_dialogueObject = null;
				continue;
			}
			DialogueController.StartDialogueSFX(DialogueAudio, LayerMask.NameToLayer("Default"));
			while (m_dialogueObject.activeInHierarchy && controller.IsPrinting())
			{
				yield return null;
			}
			DialogueController.StopDialogueSFX(DialogueAudio);
			while (true)
			{
				yield return null;
			}
		}
	}

	public void IgnoreInput(bool _ignoreInput)
	{
		m_ignoreInput = _ignoreInput;
	}

	public void NextDialog()
	{
		m_moveToNextDialog = true;
	}

	public void Shutdown()
	{
		DialogueController.StopDialogueSFX(DialogueAudio);
		if ((bool)m_dialogueObject)
		{
			UnityEngine.Object.Destroy(m_dialogueObject);
			m_dialogueObject = null;
		}
	}
}
