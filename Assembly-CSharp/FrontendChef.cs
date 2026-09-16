using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class FrontendChef : MonoBehaviour
{
	public enum AnimationSet
	{
		One = 0,
		Two = 1,
		Three = 2,
		Four = 3
	}

	private static class Uniforms
	{
		internal static readonly int _AmbientLighting = Shader.PropertyToID("_AmbientLighting");
	}

	public enum ShaderMode
	{
		eStandard = 0,
		eUI = 1
	}

	[SerializeField]
	private Shader m_StandardSkinShader;

	[SerializeField]
	private Shader m_StandardClothesShader;

	[SerializeField]
	private Shader m_UISkinShader;

	[SerializeField]
	private Shader m_UIClothesShader;

	private ChefMeshReplacer m_chefMeshReplacer;

	private Transform[] m_props;

	private FrontendChefCustomisation m_ChefCustomisation;

	private FastList<Renderer> m_ChefRenderers = new FastList<Renderer>(30);

	private FastList<FastList<Material>> m_chefRendererMats = new FastList<FastList<Material>>(30);

	private Animator m_ChefAnimator;

	private HatMeshVisibility m_HatMeshVisibility;

	private GameSession.SelectedChefData m_SelectedChefData;

	private bool m_bForce;

	private int m_AnimationSetVariable = Animator.StringToHash("AnimationSet");

	private bool m_bHasAnimationSet;

	private AnimationSet m_AnimationSet;

	private HatMeshVisibility.VisState m_HatState;

	private ShaderMode m_CurrentShaderMode;

	private Color m_CurrentUIAmbientColor = Color.black;

	public GameObject ChefModel
	{
		get
		{
			return (!(m_chefMeshReplacer != null)) ? null : m_chefMeshReplacer.ChefModel;
		}
	}

	public ShaderMode CurrentShaderMode
	{
		get
		{
			return m_CurrentShaderMode;
		}
	}

	private void Awake()
	{
		m_chefMeshReplacer = base.gameObject.RequireComponent<ChefMeshReplacer>();
		m_ChefCustomisation = base.gameObject.RequestComponent<FrontendChefCustomisation>();
		m_HatMeshVisibility = base.gameObject.RequestComponent<HatMeshVisibility>();
		SetChefData();
	}

	public void SetChefData(GameSession.SelectedChefData chefData, bool force = false)
	{
		m_SelectedChefData = chefData;
		m_bForce = force;
		SetChefData();
	}

	private void SetChefData()
	{
		if (!(m_chefMeshReplacer != null) || m_SelectedChefData == null)
		{
			return;
		}
		GameObject chefModel = m_chefMeshReplacer.ChefModel;
		m_chefMeshReplacer.SetChefData(m_SelectedChefData, m_bForce);
		if (chefModel != m_chefMeshReplacer.ChefModel)
		{
			m_ChefRenderers.Clear();
			for (int i = 0; i < m_chefRendererMats.Count; i++)
			{
				if (m_chefRendererMats._items[i] != null)
				{
					m_chefRendererMats._items[i].Clear();
				}
			}
		}
		if (m_chefMeshReplacer.ChefModel != null)
		{
			if (chefModel != m_chefMeshReplacer.ChefModel)
			{
				UpdateChefRenderers();
				UpdateHatRenderers();
			}
			SetCorrectHat();
			m_ChefAnimator = m_chefMeshReplacer.ChefModel.RequireComponent<Animator>();
			if (m_ChefCustomisation != null)
			{
				m_bHasAnimationSet = m_ChefAnimator.HasParameter(m_AnimationSetVariable);
				m_ChefCustomisation.SetChefAnimator(m_ChefAnimator);
			}
			SetCorrectAnimations();
		}
		if (chefModel != m_chefMeshReplacer.ChefModel)
		{
			HideProps(true);
			SetCorrectShaders();
		}
	}

	protected void HideProps(bool _updateAnimator)
	{
		if (!FindProps())
		{
			return;
		}
		for (int i = 0; i < m_props.Length; i++)
		{
			m_props[i].gameObject.SetActive(false);
		}
		if (_updateAnimator)
		{
			if (m_ChefAnimator == null && m_chefMeshReplacer.ChefModel != null)
			{
				m_ChefAnimator = m_chefMeshReplacer.ChefModel.RequireComponent<Animator>();
				m_bHasAnimationSet = m_ChefAnimator.HasParameter(m_AnimationSetVariable);
			}
			if (m_ChefAnimator != null)
			{
				m_ChefAnimator.Update(Time.deltaTime);
			}
		}
	}

	public void SetChefHat(HatMeshVisibility.VisState _hat)
	{
		m_HatState = _hat;
		SetCorrectHat();
	}

	public void SetAnimationSet(AnimationSet _animSet)
	{
		m_AnimationSet = _animSet;
		SetCorrectAnimations();
	}

	public void SetShaderMode(ShaderMode eShaderMode)
	{
		m_CurrentShaderMode = eShaderMode;
		SetCorrectShaders();
	}

	public void SetUIChefAmbientLighting(Color ambientColor)
	{
		m_CurrentUIAmbientColor = ambientColor;
		ApplyAmbientLighting();
	}

	private bool FindProps()
	{
		m_props = null;
		Transform transform = base.transform.FindChildRecursive("Props");
		if (transform != null)
		{
			int childCount = transform.childCount;
			if (childCount > 0)
			{
				m_props = new Transform[childCount];
				for (int i = 0; i < childCount; i++)
				{
					m_props[i] = transform.GetChild(i);
				}
				return true;
			}
		}
		return false;
	}

	private void UpdateChefRenderers()
	{
		if (!(m_chefMeshReplacer.ChefModel != null))
		{
			return;
		}
		Renderer[] collection = m_chefMeshReplacer.ChefModel.RequestComponentsRecursive<Renderer>();
		m_ChefRenderers.Clear();
		m_ChefRenderers.AddRange(collection);
		int count = m_ChefRenderers.Count;
		int count2 = m_chefRendererMats.Count;
		if (count2 > count)
		{
			m_chefRendererMats.RemoveRange(count - 1, count2 - count);
		}
		for (int i = 0; i < count; i++)
		{
			Material[] materials = m_ChefRenderers._items[i].materials;
			if (i > m_chefRendererMats.Count - 1)
			{
				FastList<Material> fastList = new FastList<Material>(materials.Length);
				fastList.AddRange(materials);
				m_chefRendererMats.Add(fastList);
				continue;
			}
			FastList<Material> fastList2 = m_chefRendererMats._items[i];
			if (fastList2 != null)
			{
				fastList2.Clear();
			}
			else
			{
				fastList2 = new FastList<Material>(materials.Length);
				m_chefRendererMats._items[i] = fastList2;
			}
			fastList2.AddRange(materials);
		}
	}

	private void UpdateHatRenderers()
	{
		if (m_chefMeshReplacer.ChefModel != null && m_HatMeshVisibility != null)
		{
			m_HatMeshVisibility.Setup(m_HatState);
		}
	}

	private void SetCorrectHat()
	{
		if (m_chefMeshReplacer.ChefModel != null && m_HatMeshVisibility != null)
		{
			m_HatMeshVisibility.SetState(m_HatState);
		}
	}

	private void SetCorrectAnimations()
	{
		if (m_ChefAnimator == null && m_chefMeshReplacer.ChefModel != null)
		{
			m_ChefAnimator = m_chefMeshReplacer.ChefModel.RequireComponent<Animator>();
			m_bHasAnimationSet = m_ChefAnimator.HasParameter(m_AnimationSetVariable);
		}
		if (m_ChefAnimator != null && m_bHasAnimationSet)
		{
			int value = (int)(m_AnimationSet + 1);
			m_ChefAnimator.SetInteger(m_AnimationSetVariable, value);
		}
	}

	private void SetCorrectShaders()
	{
		if (m_StandardSkinShader == null || m_StandardClothesShader == null || m_UIClothesShader == null || m_UISkinShader == null)
		{
			return;
		}
		if (m_ChefRenderers == null)
		{
			UpdateChefRenderers();
			UpdateHatRenderers();
		}
		if (m_ChefRenderers != null)
		{
			for (int i = 0; i < m_ChefRenderers.Count; i++)
			{
				Renderer renderer = m_ChefRenderers._items[i];
				if (!(renderer != null))
				{
					continue;
				}
				FastList<Material> fastList = m_chefRendererMats._items[i];
				for (int j = 0; j < fastList.Count; j++)
				{
					Material material = fastList._items[j];
					if (m_CurrentShaderMode == ShaderMode.eStandard)
					{
						renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
						renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
						if (material.shader == m_UIClothesShader)
						{
							material.shader = m_StandardClothesShader;
						}
						else if (material.shader == m_UISkinShader)
						{
							material.shader = m_StandardSkinShader;
						}
					}
					else if (m_CurrentShaderMode == ShaderMode.eUI)
					{
						renderer.lightProbeUsage = LightProbeUsage.Off;
						renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
						if (material.shader == m_StandardClothesShader)
						{
							material.shader = m_UIClothesShader;
						}
						else if (material.shader == m_StandardSkinShader)
						{
							material.shader = m_UISkinShader;
						}
					}
				}
			}
		}
		if (m_CurrentShaderMode == ShaderMode.eUI)
		{
			SetUIChefAmbientLighting(m_CurrentUIAmbientColor);
		}
	}

	private void ApplyAmbientLighting()
	{
		if (m_ChefRenderers == null)
		{
			return;
		}
		Color currentUIAmbientColor = m_CurrentUIAmbientColor;
		for (int i = 0; i < m_ChefRenderers.Count; i++)
		{
			FastList<Material> fastList = m_chefRendererMats._items[i];
			for (int j = 0; j < fastList.Count; j++)
			{
				fastList._items[j].SetColor(Uniforms._AmbientLighting, m_CurrentUIAmbientColor);
			}
		}
	}

	private void OnEnable()
	{
		SetCorrectAnimations();
	}

	private void OnDisable()
	{
		AutoDestructParticleSystem[] array = base.gameObject.RequestComponentsRecursive<AutoDestructParticleSystem>();
		for (int num = array.Length - 1; num >= 0; num--)
		{
			Object.Destroy(array[num].gameObject);
		}
	}
}
