using System.Collections;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ClientPlateStation : ClientSynchroniserBase
{
	private const float NEEDS_PLATE_COOLDOWN_TIME = 1f;

	private PlateStation m_plateStation;

	private ClientAttachStation m_ClientAttachStation;

	private float m_needsPlateCooldownTimer = -1f;

	private DataStore m_dataStore;

	private static readonly DataStore.Id k_scoreTipId = new DataStore.Id("score.tip");

	private PlateStationMessage m_data = new PlateStationMessage();

	private AttachStation m_attachStation;

	private WaitForSeconds m_waitForPfxDelay;

	public override EntityType GetEntityType()
	{
		return EntityType.PlateStation;
	}

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_ClientAttachStation = GetComponent<ClientAttachStation>();
		m_ClientAttachStation.RegisterAllowItemPlacement(CanAddItem);
	}

	private void OnScoreTipNotification(DataStore.Id id, object data)
	{
		TeamTip teamTip = (TeamTip)data;
		if (teamTip.m_tip > 0 && teamTip.m_team == m_plateStation.m_teamId && teamTip.m_station == this)
		{
			GameObject obj = GameUtils.InstantiateHoverIconUIController(m_plateStation.m_tipsFloatingNumberUI, GetAttachPoint(base.gameObject), "HoverIconCanvas");
			DisplayIntUIController displayIntUIController = obj.RequireComponent<DisplayIntUIController>();
			displayIntUIController.Value = teamTip.m_tip;
		}
	}

	public override void ApplyServerEvent(Serialisable serialisable)
	{
		PlateStationMessage plateStationMessage = (PlateStationMessage)serialisable;
		if (plateStationMessage.m_success)
		{
			ClientPlate plate = plateStationMessage.m_delivered.RequireComponent<ClientPlate>();
			DeliverPlate(plate);
		}
		else
		{
			FailedToDeliver(plateStationMessage.m_delivered);
		}
	}

	public override void UpdateSynchronising()
	{
		if (m_needsPlateCooldownTimer > 0f)
		{
			m_needsPlateCooldownTimer -= TimeManager.GetDeltaTime(base.gameObject);
		}
	}

	private void DeliverPlate(ClientPlate _plate)
	{
		GameUtils.TriggerAudio(GameOneShotAudioTag.ServiceBell, base.gameObject.layer);
		StartCoroutine(DeliverySequence(_plate, m_plateStation.m_deliveryEffects));
		if (m_plateStation.m_onFoodDeliveredTrigger != string.Empty)
		{
			base.gameObject.SendTrigger(m_plateStation.m_onFoodDeliveredTrigger);
		}
	}

	private IEnumerator DeliverySequence(ClientPlate plate, PlateStation.DeliveryFX deliveryFx)
	{
		m_waitForPfxDelay = new WaitForSeconds(deliveryFx.m_pfxToFadeDelayTime);
		Collider[] colliders = plate.gameObject.RequestComponentsRecursive<Collider>();
		foreach (Collider collider in colliders)
		{
			collider.enabled = false;
		}
		Rigidbody rigidBody = plate.gameObject.RequestComponent<Rigidbody>();
		if (rigidBody != null)
		{
			rigidBody.isKinematic = true;
		}
		if (deliveryFx.m_deliverPFXPrefab != null)
		{
			GameObject pfx = deliveryFx.m_deliverPFXPrefab.InstantiateOnParent(base.transform);
			pfx.transform.SetParent(null);
			yield return m_waitForPfxDelay;
		}
		MeshRenderer[] allRenderers = plate.gameObject.RequestComponentsRecursive<MeshRenderer>();
		for (int j = 0; j < allRenderers.Length; j++)
		{
			MeshRenderer meshRenderer = allRenderers[j];
			if (!meshRenderer.material.HasProperty("_Mode"))
			{
				Material material = new Material(meshRenderer.material);
				material.shader = deliveryFx.m_fadeOutShader;
				Material material2 = material;
				meshRenderer.material = material2;
			}
			else
			{
				meshRenderer.material.SetFloat("_Mode", 2f);
				StandardShaderHelper.SetupMaterialWithBlendMode(allRenderers[j].material, StandardShaderHelper.BlendMode.Fade);
			}
		}
		bool errored = false;
		float progress = 0f;
		while (progress < 1f)
		{
			foreach (MeshRenderer meshRenderer2 in allRenderers)
			{
				if (meshRenderer2 == null)
				{
					if (!errored)
					{
						errored = true;
					}
				}
				else if (meshRenderer2.material.HasProperty("_Alpha"))
				{
					float value = 1f - progress;
					meshRenderer2.material.SetFloat("_Alpha", value);
				}
			}
			progress += TimeManager.GetDeltaTime(plate.gameObject) / deliveryFx.m_fadeTime;
			yield return null;
		}
		Object.Destroy(plate.gameObject);
	}

	private void FailedToDeliver(GameObject _object)
	{
		GameUtils.TriggerAudio(GameOneShotAudioTag.UIBack, base.gameObject.layer);
		if (m_needsPlateCooldownTimer <= 0f && _object != null && _object.GetComponent<Plate>() == null)
		{
			GameUtils.InstantiateHoverIconUIController(m_plateStation.m_needsPlateFloatingUI, m_attachStation.GetAttachPoint(_object), "HoverIconCanvas");
			m_needsPlateCooldownTimer = 1f;
		}
	}

	public Transform GetAttachPoint(GameObject gameObject)
	{
		return m_attachStation.GetAttachPoint(gameObject);
	}

	private void Awake()
	{
		m_attachStation = base.gameObject.GetComponent<AttachStation>();
		m_plateStation = base.gameObject.RequireComponent<PlateStation>();
		m_dataStore = GameUtils.RequireManager<DataStore>();
		m_dataStore.Register(k_scoreTipId, OnScoreTipNotification);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (null != m_ClientAttachStation)
		{
			m_ClientAttachStation.UnregisterAllowItemPlacement(CanAddItem);
		}
		if (m_dataStore != null)
		{
			m_dataStore.Unregister(k_scoreTipId, OnScoreTipNotification);
		}
	}

	private bool CanAddItem(GameObject _object, PlacementContext _context)
	{
		return _object.GetComponent<ClientPlate>() != null;
	}
}
