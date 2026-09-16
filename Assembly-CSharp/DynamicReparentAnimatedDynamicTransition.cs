using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DynamicReparentAnimatedDynamicTransition : AnimatedDynamicTransition
{
	[SerializeField]
	private GameObject dynamicParent;

	public override IEnumerator Run()
	{
		GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
		foreach (GameObject gameObject in rootObjects)
		{
			if (ConnectionStatus.IsHost() || !ConnectionStatus.IsInSession())
			{
				IAttachment[] componentsInChildren = gameObject.GetComponentsInChildren<IAttachment>();
				for (int j = 0; j < componentsInChildren.Length; j++)
				{
					if (componentsInChildren[j] != null && !(componentsInChildren[j].AccessGameObject() == null) && !componentsInChildren[j].IsAttached())
					{
						ServerPlayerRespawnManager.KillOrRespawn(componentsInChildren[j].AccessGameObject(), null);
					}
				}
				PlayerControls[] componentsInChildren2 = gameObject.GetComponentsInChildren<PlayerControls>();
				for (int k = 0; k < componentsInChildren2.Length; k++)
				{
					componentsInChildren2[k].transform.SetParent(null);
				}
			}
			DynamicLandscapeParenting[] componentsInChildren3 = gameObject.GetComponentsInChildren<DynamicLandscapeParenting>();
			for (int l = 0; l < componentsInChildren3.Length; l++)
			{
				componentsInChildren3[l].enabled = false;
				componentsInChildren3[l].SetEnabled(false);
			}
		}
		IEnumerator routine = _003CRun_003E__BaseCallProxy0();
		while (routine.MoveNext())
		{
			yield return null;
		}
	}

	[CompilerGenerated]
	[DebuggerHidden]
	private IEnumerator _003CRun_003E__BaseCallProxy0()
	{
		return base.Run();
	}
}
