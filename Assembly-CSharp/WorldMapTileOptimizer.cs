using UnityEngine;

[ExecutionDependency(typeof(WorldMapFlipperBase))]
[RequireComponent(typeof(WorldMapTileFlip))]
public class WorldMapTileOptimizer : MonoBehaviour, ITileFlipAnimatorProvider, ITileFlipStaticHandler, ITileFlipStartup
{
	[SerializeField]
	private Mesh m_flippedMesh;

	[SerializeField]
	private Mesh m_unflippedMesh;

	[HideInInspector]
	[SerializeField]
	private TileCosmetics m_cosmetics;

	[SerializeField]
	private bool m_startStatic;

	[SerializeField]
	[AssignChild("Tile", Editorbility.NonEditable)]
	private GameObject m_tile;

	[SerializeField]
	private RuntimeAnimatorController m_controller;

	[SerializeField]
	private bool m_debugBreak;

	private MeshRenderer m_MeshRenderer;

	private Material m_OriginalMaterial;

	private StaticGridLocation m_staticGridLocation;

	private Animator m_animator;

	private AnimatorCommunications m_animatorComs;

	private bool m_complete;

	public TileCosmetics Cosmetics
	{
		get
		{
			return m_cosmetics;
		}
		set
		{
			m_cosmetics = value;
		}
	}

	public GameObject Tile
	{
		get
		{
			return m_tile;
		}
	}

	protected virtual void Awake()
	{
		UpdateMeshRenderer();
		WorldMapMaterialPool.RegisterMaterial(m_OriginalMaterial);
	}

	public Animator Begin(FlipDirection _direction)
	{
		m_tile.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
		m_animator = m_tile.AddComponent<Animator>();
		m_animator.runtimeAnimatorController = m_controller;
		m_animatorComs = m_tile.AddComponent<AnimatorCommunications>();
		return m_animator;
	}

	public void End(FlipDirection _direction)
	{
		if (m_animatorComs != null)
		{
			Object.Destroy(m_animatorComs);
			m_animatorComs = null;
		}
		if (m_animator != null)
		{
			Object.Destroy(m_animator);
			m_animator = null;
		}
		UpdateMesh();
		m_complete = true;
	}

	public bool IsComplete()
	{
		return m_complete;
	}

	public void StartUp()
	{
		if (m_debugBreak)
		{
		}
		if (!m_complete)
		{
			m_tile.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
			if (m_MeshRenderer != null)
			{
				m_MeshRenderer.material.SetFloat("_Blend", 0f);
			}
		}
		if (!m_startStatic)
		{
		}
	}

	public void SetAsStatic()
	{
		if (!m_complete)
		{
			End(FlipDirection.Unfold);
			return;
		}
		m_tile.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
		UpdateMesh();
	}

	protected void UpdateMesh()
	{
		if (m_MeshRenderer == null)
		{
			UpdateMeshRenderer();
		}
		MeshFilter meshFilter = m_MeshRenderer.gameObject.RequireComponent<MeshFilter>();
		WorldMapTileFlip worldMapTileFlip = base.gameObject.RequestComponent<WorldMapTileFlip>();
		meshFilter.sharedMesh = ((!worldMapTileFlip.IsFlipped()) ? m_unflippedMesh : m_flippedMesh);
		if (worldMapTileFlip.IsFlipped())
		{
			m_MeshRenderer.sharedMaterial = WorldMapMaterialPool.GetSharedMaterialForState(m_OriginalMaterial, WorldMapMaterialPool.MapState.UnFolded);
		}
		else
		{
			m_MeshRenderer.sharedMaterial = WorldMapMaterialPool.GetSharedMaterialForState(m_OriginalMaterial, WorldMapMaterialPool.MapState.Folded);
		}
	}

	private void UpdateMeshRenderer()
	{
		if (m_MeshRenderer == null)
		{
			m_MeshRenderer = base.gameObject.RequestComponentInImmediateChildren<MeshRenderer>();
			if (m_MeshRenderer == null)
			{
				m_MeshRenderer = m_tile.RequestComponentInImmediateChildren<MeshRenderer>();
			}
			m_OriginalMaterial = m_MeshRenderer.material;
		}
	}
}
