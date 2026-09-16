using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GraveyardAnimatedDynamicTransition : AnimatedDynamicTransition
{
	[Header("Outro Cutscenes")]
	[SerializeField]
	private CutsceneController m_newSuccessCutscene;

	[SerializeField]
	private CutsceneController m_newFailureCutscene;

	public override IEnumerator Run()
	{
		Suppressor serverSuppressor = null;
		Suppressor clientSuppressor = null;
		FlowControllerBase flowController = GameUtils.RequireManager<FlowControllerBase>();
		ServerKitchenFlowControllerBase serverFlow = flowController.gameObject.RequestComponent<ServerKitchenFlowControllerBase>();
		if (serverFlow != null && serverFlow.RoundTimer != null)
		{
			serverSuppressor = serverFlow.RoundTimer.Suppressor.AddSuppressor(this);
		}
		ClientKitchenFlowControllerBase clientFlow = flowController.gameObject.RequestComponent<ClientKitchenFlowControllerBase>();
		if (clientFlow != null && clientFlow.RoundTimer != null)
		{
			clientSuppressor = clientFlow.RoundTimer.Suppressor.AddSuppressor(this);
		}
		Canvas hoverIconCanvas = GameUtils.GetNamedCanvas("HoverIconCanvas").RequireComponent<Canvas>();
		hoverIconCanvas.enabled = false;
		GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
		foreach (GameObject gameObject in rootObjects)
		{
			if (!ConnectionStatus.IsHost() && ConnectionStatus.IsInSession())
			{
				continue;
			}
			IAttachment[] componentsInChildren = gameObject.GetComponentsInChildren<IAttachment>();
			for (int j = 0; j < componentsInChildren.Length; j++)
			{
				if (componentsInChildren[j] != null && !(componentsInChildren[j].AccessGameObject() == null) && !componentsInChildren[j].IsAttached())
				{
					NetworkUtils.DestroyObject(componentsInChildren[j].AccessGameObject());
				}
			}
		}
		IEnumerator transitionRoutine = _003CRun_003E__BaseCallProxy0();
		while (transitionRoutine.MoveNext())
		{
			yield return transitionRoutine.Current;
		}
		hoverIconCanvas.enabled = true;
		if (serverSuppressor != null)
		{
			serverSuppressor.Release();
		}
		if (clientSuppressor != null)
		{
			clientSuppressor.Release();
		}
		BossLevelOutroFlowroutine bossOutro = flowController.gameObject.RequireComponent<BossLevelOutroFlowroutine>();
		bossOutro.SetOutroDirectors(m_newSuccessCutscene, m_newFailureCutscene);
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private IEnumerator _003CRun_003E__BaseCallProxy0()
	{
		return base.Run();
	}
}
