using UnityEngine;

public class DirtyPlateStackCosmetics : MonoBehaviour
{
	private int m_blendID = Shader.PropertyToID("_BlendFactor");

	private const float kProgressOffset = 0.5f;

	private ClientWashable m_washable;

	private ClientDirtyPlateStack m_plateStack;

	private MaterialPropertyBlock m_propertyBlock;

	private Renderer[] m_renderers;

	private float m_lastProgress;

	private bool m_refreshProgress;

	private void Start()
	{
		if (m_washable == null)
		{
			SetupWashable();
		}
		if (m_plateStack == null)
		{
			SetupPlateStack();
		}
		m_propertyBlock = new MaterialPropertyBlock();
	}

	private void SetupWashable()
	{
		m_washable = base.gameObject.RequestComponent<ClientWashable>();
	}

	private void SetupPlateStack()
	{
		m_plateStack = base.gameObject.RequestComponent<ClientDirtyPlateStack>();
		if (m_plateStack != null)
		{
			m_plateStack.RegisterOnPlateAdded(PlateChanged);
			m_plateStack.RegisterOnPlateRemoved(PlateChanged);
			UpdateRenderers();
		}
	}

	private void PlateChanged(GameObject _plate)
	{
		UpdateRenderers();
	}

	private void UpdateRenderers()
	{
		m_renderers = base.gameObject.RequestComponentsRecursive<Renderer>();
		m_refreshProgress = true;
	}

	private void Update()
	{
		if (m_washable == null)
		{
			SetupWashable();
		}
		if (m_plateStack == null)
		{
			SetupPlateStack();
		}
		if (!(m_washable != null) || m_renderers == null)
		{
			return;
		}
		float progressPercent = m_washable.ProgressPercent;
		float num = Mathf.Clamp01(Mathf.SmoothStep(0f, 1f, progressPercent) - 0.5f);
		if (m_lastProgress != num || m_refreshProgress)
		{
			m_propertyBlock.SetFloat(m_blendID, num);
			for (int i = 0; i < m_renderers.Length; i++)
			{
				Renderer renderer = m_renderers[i];
				renderer.SetPropertyBlock(m_propertyBlock);
			}
			m_lastProgress = num;
			m_refreshProgress = false;
		}
	}

	private void OnDestroy()
	{
		if (m_plateStack != null)
		{
			m_plateStack.UnregisterOnPlateAdded(PlateChanged);
			m_plateStack.UnregisterOnPlateRemoved(PlateChanged);
		}
	}
}
