using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PromptDialogueController : UIControllerBase
{
	[SerializeField]
	[AssignChild("Title", Editorbility.NonEditable)]
	private Text m_titleObject;

	[SerializeField]
	[AssignChild("ExplainationText", Editorbility.NonEditable)]
	private Text m_explanationTextObject;

	[SerializeField]
	[AssignComponentRecursive(Editorbility.NonEditable)]
	private FrontendGUI m_frontendGUI;

	[SerializeField]
	private bool m_autoBegin;

	[SerializeField]
	[HideInInspectorTest("m_autoBegin", true)]
	private string m_titleText;

	[SerializeField]
	[HideInInspectorTest("m_autoBegin", true)]
	private string m_explanationText;

	private IEnumerator m_iterator;

	public event VoidGeneric<bool> ResultCallback = delegate
	{
	};

	private void Awake()
	{
		if (m_autoBegin)
		{
			Begin(m_titleText, m_explanationText);
		}
	}

	public void Begin(string _title, string _explanation)
	{
		m_titleObject.text = _title;
		m_explanationTextObject.text = _explanation;
		m_iterator = Run(m_frontendGUI);
	}

	private void Update()
	{
		if (m_iterator != null && !m_iterator.MoveNext())
		{
			m_iterator = null;
		}
	}

	public IEnumerator Run(FrontendGUI _ui)
	{
		ScrollingListControlsHelper scrollingControlsHelper = new ScrollingListControlsHelper();
		scrollingControlsHelper.Init(_ui, 0f);
		bool finished = false;
		scrollingControlsHelper.RegisterSelectionCallback(delegate
		{
			finished = true;
		});
		while (!finished)
		{
			scrollingControlsHelper.Update();
			yield return null;
		}
		Object.Destroy(base.gameObject);
	}
}
