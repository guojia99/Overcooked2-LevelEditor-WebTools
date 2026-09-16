using System.Collections.Generic;
using UnityEngine;

[ExecutionDependency(typeof(PlayerIDProvider))]
[ExecutionDependency(typeof(KitchenBootstrapManager))]
[ExecutionDependency(typeof(PlayerControls))]
[ExecutionDependency(typeof(CampaignKitchenLoaderManager))]
[ExecutionDependency(typeof(CompetitiveKitchenLoaderManager))]
public class ChefMeshReplacer : MonoBehaviour
{
	public enum ChefColourisationMode
	{
		SwapMaterial = 0,
		SwapColourValue = 1
	}

	public enum ChefModelType
	{
		InGame = 0,
		FrontEnd = 1,
		UI = 2
	}

	private struct ChefParts
	{
		public SkinnedMeshRenderer m_head;

		public List<SkinnedMeshRenderer> m_hands;
	}

	private const string c_headPrefix = "Chef_";

	private const string c_handPrefix = "Hand_";

	[SerializeField]
	private GameObject m_chefTemplatePrefab;

	[SerializeField]
	private ChefModelType m_modelType;

	private string m_currentHeadName;

	private ChefColourData m_currentColour;

	private Dictionary<string, ChefParts> m_chefParts = new Dictionary<string, ChefParts>(20);

	private FastList<SkinnedMeshRenderer> m_allHeads = new FastList<SkinnedMeshRenderer>(20);

	private GameSession.SelectedChefData chefDataCopy;

	private GameObject m_lastModelPrefab;

	private GameObject m_cachedBody;

	private GameObject m_cachedChefModel;

	private PlayerIDProvider m_playerIDProvider;

	public const string c_maskParamName = "_MaskColor";

	private int m_maskParamID;

	public GameObject ChefModel
	{
		get
		{
			return m_cachedChefModel;
		}
	}

	public GameSession.SelectedChefData GetChefData()
	{
		return chefDataCopy;
	}

	private void Awake()
	{
		m_maskParamID = Shader.PropertyToID("_MaskColor");
	}

	public void SetChefData(GameSession.SelectedChefData chefData, bool force = false)
	{
		if (chefData == null)
		{
			return;
		}
		string headName = chefData.Character.HeadName;
		ChefColourData colour = chefData.Colour;
		chefDataCopy = chefData;
		bool flag = headName != m_currentHeadName;
		if (force || flag || colour != m_currentColour)
		{
			ReplaceModel(chefData, force);
			if (force || flag)
			{
				SetHeadVisibility(chefData.Character.HeadName);
			}
			m_currentHeadName = headName;
			m_currentColour = colour;
		}
	}

	private void SetHeadVisibility(string _headName)
	{
		if (m_chefParts.Count == 0)
		{
			CacheChefParts();
		}
		ChefParts chefParts = m_chefParts.SafeGet(_headName);
		SkinnedMeshRenderer head = chefParts.m_head;
		List<SkinnedMeshRenderer> hands = chefParts.m_hands;
		for (int i = 0; i < m_allHeads.Count; i++)
		{
			m_allHeads._items[i].gameObject.SetActive(false);
		}
		if (head != null)
		{
			head.gameObject.SetActive(true);
			for (int j = 0; j < hands.Count; j++)
			{
				hands[j].sharedMaterial = head.sharedMaterial;
			}
		}
	}

	private void ReplaceModel(GameSession.SelectedChefData _chefData, bool _force)
	{
		if (m_cachedChefModel == null)
		{
			m_cachedChefModel = base.transform.FindChildRecursive("Chef").gameObject;
		}
		if (m_currentHeadName == _chefData.Character.HeadName && m_currentColour != _chefData.Colour && !_force)
		{
			AssignBodyColour(m_cachedChefModel, _chefData);
			return;
		}
		GameObject gameObject = null;
		switch (m_modelType)
		{
		case ChefModelType.InGame:
			gameObject = _chefData.Character.ModelPrefab;
			break;
		case ChefModelType.FrontEnd:
			gameObject = _chefData.Character.FrontendModelPrefab;
			break;
		case ChefModelType.UI:
			gameObject = _chefData.Character.UIModelPrefab;
			break;
		}
		if (!(m_lastModelPrefab != gameObject))
		{
			return;
		}
		m_lastModelPrefab = gameObject;
		GameObject gameObject2 = Object.Instantiate(gameObject);
		gameObject2.SetObjectLayer(base.gameObject.layer);
		gameObject2.name = m_cachedChefModel.name;
		gameObject2.transform.SetParent(m_cachedChefModel.transform.parent, false);
		gameObject2.transform.localPosition = m_cachedChefModel.transform.localPosition;
		gameObject2.transform.localRotation = m_cachedChefModel.transform.localRotation;
		gameObject2.transform.localScale = m_cachedChefModel.transform.localScale;
		Animator animator = m_chefTemplatePrefab.RequireComponent<Animator>();
		Animator animator2 = gameObject2.RequireComponent<Animator>();
		animator2.runtimeAnimatorController = animator.runtimeAnimatorController;
		AnimationEventData other = m_chefTemplatePrefab.RequireComponent<AnimationEventData>();
		AnimationEventData animationEventData = gameObject2.RequestComponent<AnimationEventData>();
		if (animationEventData == null)
		{
			animationEventData = gameObject2.AddComponent<AnimationEventData>();
		}
		animationEventData.Copy(other);
		if (gameObject2.RequestComponent<AnimatorRumbleComponent>() == null)
		{
			gameObject2.AddComponent<AnimatorRumbleComponent>();
		}
		if (gameObject2.RequestComponent<AnimatorCommunications>() == null)
		{
			gameObject2.AddComponent<AnimatorCommunications>();
		}
		if (gameObject2.RequestComponent<AnimatorAudioComponent>() == null)
		{
			gameObject2.AddComponent<AnimatorAudioComponent>();
		}
		if (gameObject2.RequestComponent<ForwardTriggersToParent>() == null)
		{
			gameObject2.AddComponent<ForwardTriggersToParent>();
		}
		if (gameObject2.RequestComponent<RendererSceneInfo>() == null)
		{
			RendererSceneInfo rendererSceneInfo = gameObject2.AddComponent<RendererSceneInfo>();
			rendererSceneInfo.m_rendererClass = RendererSceneSettings.RendererClass.Avatar;
		}
		AssignBodyColour(gameObject2, _chefData);
		if (m_modelType == ChefModelType.InGame)
		{
			CreateAttachPointFromTemplate(gameObject2, "Attachment");
			CreateAttachPointFromTemplate(gameObject2, "Slip_Particles");
			CreateAttachPointFromTemplate(gameObject2, "Attachment_Backpack");
			Transform[] componentsInChildren = gameObject2.GetComponentsInChildren<Transform>(true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				ComponentCacheRegistry.UpdateObject(componentsInChildren[i].gameObject);
			}
		}
		Animator animator3 = m_cachedChefModel.RequestComponent<Animator>();
		if (animator3 != null && animator3.runtimeAnimatorController != null)
		{
			AnimatorStateInfo currentAnimatorStateInfo = animator3.GetCurrentAnimatorStateInfo(0);
			animator2.Play(currentAnimatorStateInfo.fullPathHash, 0, currentAnimatorStateInfo.normalizedTime);
			animator2.Update(0f);
		}
		Object.DestroyImmediate(m_cachedChefModel);
		GameObject cachedBody = gameObject2.transform.FindChildRecursive("Body").gameObject;
		m_cachedChefModel = gameObject2;
		m_cachedBody = cachedBody;
		CacheChefParts();
	}

	private void AssignBodyColour(GameObject _root, GameSession.SelectedChefData _data)
	{
		GameObject obj = _root.transform.FindChildRecursive("Body").gameObject;
		if (_data.Character.ColourisationMode == ChefColourisationMode.SwapMaterial)
		{
			obj.RequireComponent<SkinnedMeshRenderer>().material = _data.Colour.ChefMaterial;
			return;
		}
		Material material = obj.RequireComponent<SkinnedMeshRenderer>().material;
		material.SetColor(m_maskParamID, _data.Colour.MaskColour);
	}

	private GameObject CreateAttachPointFromTemplate(GameObject _root, string _templateObjectName)
	{
		Transform transform = m_chefTemplatePrefab.transform.FindChildRecursive(_templateObjectName);
		Transform transform2 = _root.transform.FindChildRecursive(transform.parent.name);
		GameObject gameObject = GameObjectUtils.CreateOnParent(transform2.gameObject, transform.gameObject.name);
		gameObject.transform.localPosition = transform.transform.localPosition;
		gameObject.transform.localRotation = transform.transform.localRotation;
		gameObject.transform.localScale = transform.transform.localScale;
		return gameObject;
	}

	private void CacheChefParts()
	{
		m_chefParts.Clear();
		m_allHeads.Clear();
		GameObject gameObject = m_cachedChefModel.RequestChild("Mesh");
		if (!(gameObject != null))
		{
			return;
		}
		SkinnedMeshRenderer[] array = gameObject.gameObject.RequestComponentsInImmediateChildren<SkinnedMeshRenderer>();
		for (int i = 0; i < array.Length; i++)
		{
			SkinnedMeshRenderer skinnedMeshRenderer = array[i];
			if (!skinnedMeshRenderer.name.Contains("Chef_"))
			{
				continue;
			}
			ChefParts value = new ChefParts
			{
				m_hands = new List<SkinnedMeshRenderer>(2),
				m_head = skinnedMeshRenderer
			};
			SkinnedMeshRenderer[] array2 = skinnedMeshRenderer.gameObject.RequestComponentsRecursive<SkinnedMeshRenderer>();
			for (int j = 0; j < array2.Length; j++)
			{
				SkinnedMeshRenderer skinnedMeshRenderer2 = array[i];
				if (skinnedMeshRenderer2.name.Contains("Hand_"))
				{
					value.m_hands.Add(skinnedMeshRenderer2);
				}
			}
			m_chefParts.Add(value.m_head.name, value);
			m_allHeads.Add(value.m_head);
		}
	}
}
