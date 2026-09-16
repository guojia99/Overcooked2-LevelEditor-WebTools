using System.Collections;
using System.Collections.Generic;
using Team17.Online;
using UnityEngine;

public class ClientTutorialIconController : ClientIconTutorialBase
{
	private TutorialIconController m_tutorialController;

	private AssembledDefinitionNode[] m_lettuceNode;

	private AssembledDefinitionNode[] m_tomatoNode;

	private AssembledDefinitionNode[] m_cucumberNode;

	private static readonly int m_iCollectLettuce = Animator.StringToHash("CollectLettuce");

	private static readonly int m_iCollectTomato = Animator.StringToHash("CollectTomato");

	private static readonly int m_iCollectCucumber = Animator.StringToHash("CollectCucumber");

	private static readonly int m_iPlaceLettuce = Animator.StringToHash("PlaceLettuce");

	private static readonly int m_iPlaceTomato = Animator.StringToHash("PlaceTomato");

	private static readonly int m_iPlaceCucumber = Animator.StringToHash("PlaceCucumber");

	private static readonly int m_iChopLettuce = Animator.StringToHash("ChopLettuce");

	private static readonly int m_iChopTomato = Animator.StringToHash("ChopTomato");

	private static readonly int m_iChopCucumber = Animator.StringToHash("ChopCucumber");

	private static readonly int m_iPlateLettuce = Animator.StringToHash("PlateLettuce");

	private static readonly int m_iPlateTomato = Animator.StringToHash("PlateTomato");

	private static readonly int m_iPlateCucumber = Animator.StringToHash("PlateCucumber");

	private static readonly int m_iService = Animator.StringToHash("Service!");

	private static readonly int m_iCompletedTutorialRecipes = Animator.StringToHash("CompletedTutorialRecipes");

	protected SemanticIconLookup m_semanticIconLookup;

	private int[] m_animatorFlags = new int[0];

	private IEnumerator m_switchTutorial;

	private GameObject m_switchTutorialUIObject;

	private bool m_hasTriggeredSwapTutorial;

	protected GameObject[] m_plates;

	private List<Transform> m_usedFollowTargets = new List<Transform>();

	private PlayerSwitchingManager m_playerSwitchingManager;

	private ClientCampaignFlowController m_clientFlowController;

	private TutorialIconController.TutorialStage m_stage;

	private static readonly int m_animParam_OrderNumber = Animator.StringToHash("OrderNumber");

	private ClientPlateStackBase m_plateStack;

	private ClientAttachStation m_plateReturnAttach;

	public override void StartSynchronising(Component synchronisedObject)
	{
		base.StartSynchronising(synchronisedObject);
		m_tutorialController = (TutorialIconController)synchronisedObject;
		m_semanticIconLookup = GameUtils.RequireManager<SemanticIconLookup>();
		m_playerSwitchingManager = GameUtils.RequireManager<PlayerSwitchingManager>();
		m_clientFlowController = m_iClientFlowController as ClientCampaignFlowController;
		m_tutorialController.m_dialogueAnimator.SetInteger(m_animParam_OrderNumber, (int)m_stage);
		m_plates = GetPlates();
		m_lettuceNode = new AssembledDefinitionNode[1]
		{
			new IngredientAssembledNode(m_tutorialController.m_lettuce)
		};
		m_tomatoNode = new AssembledDefinitionNode[1]
		{
			new IngredientAssembledNode(m_tutorialController.m_tomato)
		};
		m_cucumberNode = new AssembledDefinitionNode[1]
		{
			new IngredientAssembledNode(m_tutorialController.m_cucumber)
		};
	}

	private void OnNewPlate(GameObject _plate)
	{
		m_plates = GetPlates();
	}

	private void OnStackAdded(IClientAttachment _iHoldable)
	{
		if (m_plateStack != null)
		{
			m_plateStack.UnregisterOnPlateAdded(OnNewPlate);
		}
		m_plateStack = _iHoldable.AccessGameObject().RequestComponent<ClientPlateStackBase>();
		if (m_plateStack != null)
		{
			m_plateStack.RegisterOnPlateAdded(OnNewPlate);
		}
	}

	private void OnEngagementChanged(EngagementSlot _s, GamepadUser _b, GamepadUser _a)
	{
		for (int i = 0; i < m_icons.Length; i++)
		{
			if (m_icons[i].Icon != null)
			{
				Object.Destroy(m_icons[i].Icon.gameObject);
			}
		}
		CreateIcons(out m_icons, out m_animatorFlags);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		GameUtils.RequireManager<PlayerManager>().EngagementChangeCallback -= OnEngagementChanged;
		if (m_plateStack != null)
		{
			m_plateStack.UnregisterOnPlateAdded(OnNewPlate);
		}
		if (m_plateReturnAttach != null)
		{
			m_plateReturnAttach.UnregisterOnItemAdded(OnStackAdded);
		}
	}

	protected void CreateIcons(out IconData[] o_iconsData, out int[] o_animatorFlags)
	{
		m_usedFollowTargets.Clear();
		o_iconsData = new IconData[13];
		o_animatorFlags = new int[13];
		Sprite icon = m_semanticIconLookup.GetIcon(SemanticIconLookup.Semantic.Pickup);
		o_iconsData[0] = new IconData(m_tutorialController, icon, m_tutorialController.m_lettuceCrate, IconActive_LettuceCrate);
		o_animatorFlags[0] = m_iCollectLettuce;
		o_iconsData[1] = new IconData(m_tutorialController, icon, m_tutorialController.m_tomatoCrate, IconActive_TomatoCrate);
		o_animatorFlags[1] = m_iCollectTomato;
		o_iconsData[2] = new IconData(m_tutorialController, icon, m_tutorialController.m_cucumberCrate, IconActive_CucumberCrate);
		o_animatorFlags[2] = m_iCollectCucumber;
		o_iconsData[3] = new IconData(m_tutorialController, m_tutorialController.m_lettuce.m_iconSprite, m_tutorialController.m_choppingboards[0], IconActive_LettuceChoppingBoard);
		o_animatorFlags[3] = m_iPlaceLettuce;
		o_iconsData[4] = new IconData(m_tutorialController, m_tutorialController.m_tomato.m_iconSprite, m_tutorialController.m_choppingboards[1], IconActive_TomatoChoppingBoard);
		o_animatorFlags[4] = m_iPlaceTomato;
		o_iconsData[5] = new IconData(m_tutorialController, m_tutorialController.m_cucumber.m_iconSprite, m_tutorialController.m_choppingboards[2], IconActive_CucumberChoppingBoard);
		o_animatorFlags[5] = m_iPlaceCucumber;
		Sprite icon2 = m_semanticIconLookup.GetIcon(SemanticIconLookup.Semantic.Chop);
		o_iconsData[6] = new IconData(m_tutorialController, icon2, m_tutorialController.m_choppingboards[0], IconActive_ChopTheLettuce);
		o_animatorFlags[6] = m_iChopLettuce;
		o_iconsData[7] = new IconData(m_tutorialController, icon2, m_tutorialController.m_choppingboards[1], IconActive_ChopTheTomato);
		o_animatorFlags[7] = m_iChopTomato;
		o_iconsData[8] = new IconData(m_tutorialController, icon2, m_tutorialController.m_choppingboards[2], IconActive_ChopTheCucumber);
		o_animatorFlags[8] = m_iChopCucumber;
		o_iconsData[9] = new IconData(m_tutorialController, m_tutorialController.m_lettuce.m_iconSprite, null, IconActive_PlateUpLettuce);
		o_animatorFlags[9] = m_iPlateLettuce;
		o_iconsData[10] = new IconData(m_tutorialController, m_tutorialController.m_tomato.m_iconSprite, null, IconActive_PlateUpTomato);
		o_animatorFlags[10] = m_iPlateTomato;
		o_iconsData[11] = new IconData(m_tutorialController, m_tutorialController.m_cucumber.m_iconSprite, null, IconActive_PlateUpCucumber);
		o_animatorFlags[11] = m_iPlateCucumber;
		if (m_stage == TutorialIconController.TutorialStage.Lettuce)
		{
			o_iconsData[12] = new IconData(m_tutorialController, m_tutorialController.m_lettuceSaladIcon, m_tutorialController.m_plateStation, IconActive_ServeSalad);
		}
		else if (m_stage == TutorialIconController.TutorialStage.LettuceTomato)
		{
			o_iconsData[12] = new IconData(m_tutorialController, m_tutorialController.m_tomatoSaladIcon, m_tutorialController.m_plateStation, IconActive_ServeSalad);
		}
		else if (m_stage == TutorialIconController.TutorialStage.LettuceTomatoCucumber)
		{
			o_iconsData[12] = new IconData(m_tutorialController, m_tutorialController.m_cucumberSaladIcon, m_tutorialController.m_plateStation, IconActive_ServeSalad);
		}
		o_animatorFlags[12] = m_iService;
	}

	protected override void OnStartTutorial()
	{
		CreateIcons(out m_icons, out m_animatorFlags);
		m_clientFlowController.RegisterOnSuccessfulDeliveryCallback(OnSuccessfulOrder);
		GameUtils.RequireManager<PlayerManager>().EngagementChangeCallback += OnEngagementChanged;
	}

	protected override void OnTutorialUpdate()
	{
		base.OnTutorialUpdate();
		if (!m_tutorialController.m_dialogueAnimator.enabled)
		{
			m_tutorialController.m_dialogueAnimator.enabled = true;
		}
		m_usedFollowTargets.Clear();
		for (int i = 0; i < m_animatorFlags.Length; i++)
		{
			if (i < m_icons.Length)
			{
				m_tutorialController.m_dialogueAnimator.SetBool(m_animatorFlags[i], m_icons[i].Icon.gameObject.activeInHierarchy);
				m_icons[i].Icon.transform.SetAsLastSibling();
			}
		}
		if (m_switchTutorial == null && DetectSwapTutorialStart())
		{
			m_hasTriggeredSwapTutorial = true;
			ClientTutorialPopupController clientTutorialPopupController = base.gameObject.RequireComponent<ClientTutorialPopupController>();
			m_switchTutorial = clientTutorialPopupController.ShowTutorial(m_tutorialController.m_swapTutorialUI, SwapTutorialDismissRoutine);
		}
		if (m_switchTutorial != null && !m_switchTutorial.MoveNext())
		{
			ClientTutorialPopupController clientTutorialPopupController2 = base.gameObject.RequireComponent<ClientTutorialPopupController>();
			clientTutorialPopupController2.Shutdown();
			m_switchTutorial = null;
			m_playerSwitchingManager.ForceSwitchToNext(PlayerInputLookup.Player.One);
		}
		if (!(m_plateReturnAttach == null))
		{
			return;
		}
		m_plateReturnAttach = m_tutorialController.m_plateReturn.gameObject.RequestComponent<ClientAttachStation>();
		if (!(m_plateReturnAttach != null))
		{
			return;
		}
		m_plateReturnAttach.RegisterOnItemAdded(OnStackAdded);
		if (m_plateStack == null)
		{
			GameObject gameObject = m_plateReturnAttach.InspectItem();
			if (gameObject != null)
			{
				OnStackAdded(gameObject.RequestInterface<IClientAttachment>());
			}
		}
	}

	protected override void OnStopTutorial()
	{
		m_animatorFlags = new int[0];
		m_switchTutorial = null;
		if (m_switchTutorialUIObject != null)
		{
			Object.Destroy(m_switchTutorialUIObject);
		}
		GameUtils.RequireManager<PlayerManager>().EngagementChangeCallback -= OnEngagementChanged;
	}

	protected void OnSuccessfulOrder(RecipeList.Entry _node)
	{
		if (m_stage == TutorialIconController.TutorialStage.Lettuce)
		{
			m_stage = TutorialIconController.TutorialStage.LettuceTomato;
			if (m_icons != null && m_icons.Length > 12 && m_icons[12].Icon != null)
			{
				m_icons[12].Icon.gameObject.SetActive(false);
			}
			for (int i = 0; i < m_icons.Length; i++)
			{
				Object.Destroy(m_icons[i].Icon.gameObject);
			}
			CreateIcons(out m_icons, out m_animatorFlags);
		}
		else if (m_stage == TutorialIconController.TutorialStage.LettuceTomato)
		{
			m_stage = TutorialIconController.TutorialStage.LettuceTomatoCucumber;
			if (m_icons != null && m_icons.Length > 12 && m_icons[12].Icon != null)
			{
				m_icons[12].Icon.gameObject.SetActive(false);
			}
			for (int j = 0; j < m_icons.Length; j++)
			{
				Object.Destroy(m_icons[j].Icon.gameObject);
			}
			CreateIcons(out m_icons, out m_animatorFlags);
		}
		else if (m_stage == TutorialIconController.TutorialStage.LettuceTomatoCucumber)
		{
			m_clientFlowController.UnregisterOnSuccessfulDeliveryCallback(OnSuccessfulOrder);
			m_tutorialController.m_dialogueAnimator.SetBool(m_iCompletedTutorialRecipes, true);
			CompleteTutorial();
		}
		m_tutorialController.m_dialogueAnimator.SetInteger(m_animParam_OrderNumber, (int)m_stage);
	}

	private bool DetectSwapTutorialStart()
	{
		if (ClientUserSystem.m_Users.Count == 1 && !m_hasTriggeredSwapTutorial)
		{
			PlayerControls controls = m_playerSwitchingManager.SelectedAvatar(PlayerInputLookup.Player.One);
			ClientWorkableItem o_workable;
			if (IsChopping(controls, out o_workable) && o_workable != null && o_workable.GetProgress() > 0.1f)
			{
				return true;
			}
		}
		return false;
	}

	private bool IsChopping(PlayerControls _controls, out ClientWorkableItem o_workable)
	{
		if (_controls != null)
		{
			ClientInteractable currentlyInteracting = _controls.GetCurrentlyInteracting();
			if ((bool)currentlyInteracting && (bool)currentlyInteracting.GetComponent<Workstation>())
			{
				ClientAttachStation clientAttachStation = currentlyInteracting.gameObject.RequireComponent<ClientAttachStation>();
				GameObject gameObject = clientAttachStation.InspectItem();
				if (gameObject != null)
				{
					o_workable = gameObject.RequestComponent<ClientWorkableItem>();
					return true;
				}
			}
		}
		o_workable = null;
		return false;
	}

	private IEnumerator SwapTutorialDismissRoutine(GameObject _ui)
	{
		ILogicalButton swapButton = PlayerInputLookup.GetButton(PlayerInputLookup.LogicalButtonID.PlayerSwitch, PlayerInputLookup.Player.One);
		while (!swapButton.JustPressed())
		{
			yield return null;
		}
	}

	protected bool InRound()
	{
		return m_clientFlowController.InRound;
	}

	protected bool IconActive_LettuceCrate(ref Transform _follower, HoverIconUIController _controller)
	{
		if (!InRound())
		{
			return false;
		}
		if (HaveGottenIngredient(m_tutorialController.m_lettuce))
		{
			return false;
		}
		if (PotentialSaladContains(m_lettuceNode))
		{
			return false;
		}
		return true;
	}

	protected bool IconActive_TomatoCrate(ref Transform _follower, HoverIconUIController _controller)
	{
		if (m_stage == TutorialIconController.TutorialStage.Lettuce)
		{
			return false;
		}
		return InRound() && !HaveGottenIngredient(m_tutorialController.m_tomato) && !PotentialSaladContains(m_tomatoNode);
	}

	protected bool IconActive_CucumberCrate(ref Transform _follower, HoverIconUIController _controller)
	{
		if (m_stage != TutorialIconController.TutorialStage.LettuceTomatoCucumber)
		{
			return false;
		}
		return InRound() && !HaveGottenIngredient(m_tutorialController.m_cucumber) && !PotentialSaladContains(m_cucumberNode);
	}

	protected bool IconActive_LettuceChoppingBoard(ref Transform _follower, HoverIconUIController _controller)
	{
		_follower = GetEmptyAttachPoint(m_tutorialController.m_choppingboards[0], m_tutorialController.m_choppingboards);
		bool flag = InRound() && IngredientToPlaceOnBoard(m_tutorialController.m_lettuce);
		if (flag)
		{
			m_usedFollowTargets.Add(_follower);
		}
		return flag;
	}

	protected bool IconActive_TomatoChoppingBoard(ref Transform _follower, HoverIconUIController _controller)
	{
		if (m_stage != TutorialIconController.TutorialStage.LettuceTomato && m_stage != TutorialIconController.TutorialStage.LettuceTomatoCucumber)
		{
			return false;
		}
		_follower = GetEmptyAttachPoint(m_tutorialController.m_choppingboards[0], m_tutorialController.m_choppingboards);
		bool flag = InRound() && IngredientToPlaceOnBoard(m_tutorialController.m_tomato);
		if (flag)
		{
			m_usedFollowTargets.Add(_follower);
		}
		return flag;
	}

	protected bool IconActive_CucumberChoppingBoard(ref Transform _follower, HoverIconUIController _controller)
	{
		if (m_stage != TutorialIconController.TutorialStage.LettuceTomatoCucumber)
		{
			return false;
		}
		_follower = GetEmptyAttachPoint(m_tutorialController.m_choppingboards[0], m_tutorialController.m_choppingboards);
		bool flag = InRound() && IngredientToPlaceOnBoard(m_tutorialController.m_cucumber);
		if (flag)
		{
			m_usedFollowTargets.Add(_follower);
		}
		return flag;
	}

	protected bool IconActive_ChopTheLettuce(ref Transform _follower, HoverIconUIController _controller)
	{
		return InRound() && ChopTheWhatever(m_tutorialController.m_lettuce, ref _follower);
	}

	protected bool IconActive_ChopTheTomato(ref Transform _follower, HoverIconUIController _controller)
	{
		if (m_stage != TutorialIconController.TutorialStage.LettuceTomato && m_stage != TutorialIconController.TutorialStage.LettuceTomatoCucumber)
		{
			return false;
		}
		return InRound() && ChopTheWhatever(m_tutorialController.m_tomato, ref _follower);
	}

	protected bool IconActive_ChopTheCucumber(ref Transform _follower, HoverIconUIController _controller)
	{
		if (m_stage != TutorialIconController.TutorialStage.LettuceTomatoCucumber)
		{
			return false;
		}
		return InRound() && ChopTheWhatever(m_tutorialController.m_cucumber, ref _follower);
	}

	protected bool IconActive_PlateUpLettuce(ref Transform _follower, HoverIconUIController _controller)
	{
		if (m_stage == TutorialIconController.TutorialStage.Lettuce)
		{
			return InRound() && PlateUpIngredientIcon(m_tutorialController.m_lettuce, new AssembledDefinitionNode[0], ref _follower, _controller, 0f);
		}
		if (m_stage == TutorialIconController.TutorialStage.LettuceTomato)
		{
			return InRound() && PlateUpIngredientIcon(m_tutorialController.m_lettuce, new AssembledDefinitionNode[1]
			{
				new IngredientAssembledNode(m_tutorialController.m_tomato)
			}, ref _follower, _controller, -30f);
		}
		return InRound() && PlateUpIngredientIcon(m_tutorialController.m_lettuce, new AssembledDefinitionNode[2]
		{
			new IngredientAssembledNode(m_tutorialController.m_tomato),
			new IngredientAssembledNode(m_tutorialController.m_cucumber)
		}, ref _follower, _controller, 0f);
	}

	protected bool IconActive_PlateUpTomato(ref Transform _follower, HoverIconUIController _controller)
	{
		if (m_stage == TutorialIconController.TutorialStage.LettuceTomato)
		{
			return InRound() && PlateUpIngredientIcon(m_tutorialController.m_tomato, new AssembledDefinitionNode[1]
			{
				new IngredientAssembledNode(m_tutorialController.m_lettuce)
			}, ref _follower, _controller, 30f);
		}
		if (m_stage == TutorialIconController.TutorialStage.LettuceTomatoCucumber)
		{
			return InRound() && PlateUpIngredientIcon(m_tutorialController.m_tomato, new AssembledDefinitionNode[2]
			{
				new IngredientAssembledNode(m_tutorialController.m_lettuce),
				new IngredientAssembledNode(m_tutorialController.m_cucumber)
			}, ref _follower, _controller, 45f);
		}
		return false;
	}

	protected bool IconActive_PlateUpCucumber(ref Transform _follower, HoverIconUIController _controller)
	{
		if (m_stage == TutorialIconController.TutorialStage.LettuceTomatoCucumber)
		{
			return InRound() && PlateUpIngredientIcon(m_tutorialController.m_cucumber, new AssembledDefinitionNode[2]
			{
				new IngredientAssembledNode(m_tutorialController.m_lettuce),
				new IngredientAssembledNode(m_tutorialController.m_tomato)
			}, ref _follower, _controller, -45f);
		}
		return false;
	}

	protected bool PlateUpIngredientIcon(IngredientOrderNode _ingredient, AssembledDefinitionNode[] _otherIngredients, ref Transform _follower, HoverIconUIController _controller, float _optionalRotation)
	{
		if (!InRound())
		{
			return false;
		}
		RectTransformExtension rectTransformExtension = _controller.gameObject.RequireComponent<RectTransformExtension>();
		rectTransformExtension.rotation = Vector3.zero;
		if (!PotentialSaladContains(new AssembledDefinitionNode[1]
		{
			new IngredientAssembledNode(_ingredient)
		}) && GameUtils.GetIngredients(_ingredient).Length > 0)
		{
			GameObject[] array = FindPotentialSalad(_otherIngredients);
			if (array.Length > 0)
			{
				rectTransformExtension.rotation = new Vector3(0f, 0f, _optionalRotation);
				_follower = array[0].transform;
				return true;
			}
			GameObject[] array2 = FindEmptyPlates();
			if (!array2.IsEmpty())
			{
				rectTransformExtension.rotation = new Vector3(0f, 0f, _optionalRotation);
				_follower = array2[0].transform;
				return true;
			}
			if (m_plates.Length > 0)
			{
				_follower = m_plates[0].transform;
				return true;
			}
		}
		return false;
	}

	protected bool IconActive_ServeSalad(ref Transform _follower, HoverIconUIController _controller)
	{
		switch (m_stage)
		{
		case TutorialIconController.TutorialStage.Lettuce:
		{
			IngredientAssembledNode ingredientAssembledNode6 = new IngredientAssembledNode(m_tutorialController.m_lettuce);
			return InRound() && !FindPotentialSalad(m_lettuceNode).IsEmpty();
		}
		case TutorialIconController.TutorialStage.LettuceTomato:
		{
			IngredientAssembledNode ingredientAssembledNode4 = new IngredientAssembledNode(m_tutorialController.m_lettuce);
			IngredientAssembledNode ingredientAssembledNode5 = new IngredientAssembledNode(m_tutorialController.m_tomato);
			return InRound() && !FindPotentialSalad(new AssembledDefinitionNode[2] { ingredientAssembledNode4, ingredientAssembledNode5 }).IsEmpty();
		}
		case TutorialIconController.TutorialStage.LettuceTomatoCucumber:
		{
			IngredientAssembledNode ingredientAssembledNode = new IngredientAssembledNode(m_tutorialController.m_lettuce);
			IngredientAssembledNode ingredientAssembledNode2 = new IngredientAssembledNode(m_tutorialController.m_tomato);
			IngredientAssembledNode ingredientAssembledNode3 = new IngredientAssembledNode(m_tutorialController.m_cucumber);
			return InRound() && !FindPotentialSalad(new AssembledDefinitionNode[3] { ingredientAssembledNode, ingredientAssembledNode2, ingredientAssembledNode3 }).IsEmpty();
		}
		default:
			return false;
		}
	}

	protected bool ChopTheWhatever(IngredientOrderNode _ingredient, ref Transform _follower)
	{
		if (!InRound())
		{
			return false;
		}
		if (PotentialSaladContains(new AssembledDefinitionNode[1]
		{
			new IngredientAssembledNode(_ingredient)
		}) || GameUtils.GetIngredients(m_tutorialController.m_lettuce).Length != 0)
		{
			return false;
		}
		for (int i = 0; i < m_tutorialController.m_choppingboards.Length; i++)
		{
			Transform transform = FindIngredientOnStation(_ingredient, m_tutorialController.m_choppingboards[i]);
			if (transform != null)
			{
				ClientWorkstation clientWorkstation = transform.gameObject.RequestComponentUpwardsRecursive<ClientWorkstation>();
				if (clientWorkstation != null && clientWorkstation.IsBeingUsed())
				{
					return false;
				}
				_follower = transform;
				return true;
			}
		}
		return false;
	}

	protected Transform FindIngredientOnStation(IngredientOrderNode _ingredient, Transform _station)
	{
		GameObject[] preIngredients = GameUtils.GetPreIngredients(_ingredient);
		ClientAttachStation clientAttachStation = _station.gameObject.RequireComponent<ClientAttachStation>();
		GameObject gameObject = clientAttachStation.InspectItem();
		if (gameObject != null && preIngredients.Contains(gameObject))
		{
			return clientAttachStation.GetComponent<AttachStation>().GetAttachPoint(gameObject);
		}
		return null;
	}

	protected Transform GetEmptyAttachPoint(Transform _default, Transform[] _stations)
	{
		ClientAttachStation clientAttachStation = _default.gameObject.RequireComponent<ClientAttachStation>();
		Transform attachPoint = clientAttachStation.GetComponent<AttachStation>().GetAttachPoint(clientAttachStation.InspectItem());
		if (!clientAttachStation.HasItem() && !m_usedFollowTargets.Contains(attachPoint))
		{
			return attachPoint;
		}
		for (int i = 0; i < _stations.Length; i++)
		{
			ClientAttachStation clientAttachStation2 = _stations[i].gameObject.RequireComponent<ClientAttachStation>();
			attachPoint = clientAttachStation2.GetComponent<AttachStation>().GetAttachPoint(clientAttachStation2.InspectItem());
			if (!clientAttachStation2.HasItem() && !m_usedFollowTargets.Contains(attachPoint))
			{
				return attachPoint;
			}
		}
		return attachPoint;
	}

	protected virtual bool IngredientToPlaceOnBoard(IngredientOrderNode _ingredient)
	{
		return GameUtils.GetPreIngredients(_ingredient).Length > 0 && !PotentialSaladContains(new AssembledDefinitionNode[1]
		{
			new IngredientAssembledNode(_ingredient)
		}) && GameUtils.GetIngredients(_ingredient).Length == 0 && !OnChoppingBoard(GameUtils.GetPreIngredients(_ingredient));
	}

	protected bool OnChoppingBoard(GameObject[] _objects)
	{
		for (int i = 0; i < m_tutorialController.m_choppingboards.Length; i++)
		{
			ClientAttachStation clientAttachStation = m_tutorialController.m_choppingboards[i].gameObject.RequireComponent<ClientAttachStation>();
			GameObject gameObject = clientAttachStation.InspectItem();
			if ((bool)gameObject && _objects.Contains(gameObject))
			{
				return true;
			}
		}
		return false;
	}

	protected bool HaveGottenIngredient(IngredientOrderNode _ingredient)
	{
		return GameUtils.GetPreIngredients(_ingredient).Length != 0 || GameUtils.GetIngredients(_ingredient).Length != 0;
	}

	protected bool PotentialSaladContains(AssembledDefinitionNode[] _ingredients)
	{
		GameObject[] array = FindPotentialSalad(_ingredients);
		return !array.IsEmpty();
	}

	public GameObject[] FindPotentialSalad(AssembledDefinitionNode[] _ingredients)
	{
		if (m_plates != null)
		{
			m_plates = m_plates.AllRemoved_Predicate((GameObject x) => x == null || !x.activeInHierarchy);
		}
		if (m_plates == null || m_plates.IsEmpty())
		{
			m_plates = GetPlates();
		}
		return GameUtils.FindContainersWithSubset("Plate", _ingredients, m_plates);
	}

	public GameObject[] FindEmptyPlates()
	{
		if (m_plates != null)
		{
			m_plates = m_plates.AllRemoved_Predicate((GameObject x) => x == null || !x.activeInHierarchy);
		}
		if (m_plates == null || m_plates.IsEmpty())
		{
			m_plates = GetPlates();
		}
		return GameUtils.FindEmptyContainers("Plate", m_plates);
	}

	protected GameObject[] GetPlates()
	{
		return GameObject.FindGameObjectsWithTag("Plate");
	}
}
