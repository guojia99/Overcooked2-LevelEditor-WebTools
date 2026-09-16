namespace UnityEngine.PostProcessing
{
	public class PostProcessingProfile : ScriptableObject
	{
		public BuiltinDebugViewsModel debugViews = new BuiltinDebugViewsModel();

		[SerializeField]
		private FogModel[] fogModels = new FogModel[PlatformUtils.s_PlatformCount];

		[SerializeField]
		private AntialiasingModel[] antialiasingModels = new AntialiasingModel[PlatformUtils.s_PlatformCount];

		[SerializeField]
		private AmbientOcclusionModel[] ambientOcclusionModels = new AmbientOcclusionModel[PlatformUtils.s_PlatformCount];

		[SerializeField]
		private ScreenSpaceReflectionModel[] screenSpaceReflectionModels = new ScreenSpaceReflectionModel[PlatformUtils.s_PlatformCount];

		[SerializeField]
		private DepthOfFieldModel[] depthOfFieldModels = new DepthOfFieldModel[PlatformUtils.s_PlatformCount];

		[SerializeField]
		private MotionBlurModel[] motionBlurModels = new MotionBlurModel[PlatformUtils.s_PlatformCount];

		[SerializeField]
		private EyeAdaptationModel[] eyeAdaptationModels = new EyeAdaptationModel[PlatformUtils.s_PlatformCount];

		[SerializeField]
		private BloomModel[] bloomModels = new BloomModel[PlatformUtils.s_PlatformCount];

		[SerializeField]
		private ColorGradingModel[] colorGradingModels = new ColorGradingModel[PlatformUtils.s_PlatformCount];

		[SerializeField]
		private UserLutModel[] userLutModels = new UserLutModel[PlatformUtils.s_PlatformCount];

		[SerializeField]
		private ChromaticAberrationModel[] chromaticAberrationModels = new ChromaticAberrationModel[PlatformUtils.s_PlatformCount];

		[SerializeField]
		private GrainModel[] grainModels = new GrainModel[PlatformUtils.s_PlatformCount];

		[SerializeField]
		private VignetteModel[] vignetteModels = new VignetteModel[PlatformUtils.s_PlatformCount];

		[SerializeField]
		private DitheringModel[] ditheringModels = new DitheringModel[PlatformUtils.s_PlatformCount];

		[SerializeField]
		private TiltShiftModel[] tiltshiftModels = new TiltShiftModel[PlatformUtils.s_PlatformCount];

		[SerializeField]
		public FogModel fog
		{
			get
			{
				return fogModels[(int)PlatformUtils.GetCurrentPlatform()];
			}
		}

		public AntialiasingModel antialiasing
		{
			get
			{
				return antialiasingModels[(int)PlatformUtils.GetCurrentPlatform()];
			}
		}

		public AmbientOcclusionModel ambientOcclusion
		{
			get
			{
				return ambientOcclusionModels[(int)PlatformUtils.GetCurrentPlatform()];
			}
		}

		public ScreenSpaceReflectionModel screenSpaceReflection
		{
			get
			{
				return screenSpaceReflectionModels[(int)PlatformUtils.GetCurrentPlatform()];
			}
		}

		public DepthOfFieldModel depthOfField
		{
			get
			{
				return depthOfFieldModels[(int)PlatformUtils.GetCurrentPlatform()];
			}
		}

		public MotionBlurModel motionBlur
		{
			get
			{
				return motionBlurModels[(int)PlatformUtils.GetCurrentPlatform()];
			}
		}

		public EyeAdaptationModel eyeAdaptation
		{
			get
			{
				return eyeAdaptationModels[(int)PlatformUtils.GetCurrentPlatform()];
			}
		}

		public BloomModel bloom
		{
			get
			{
				return bloomModels[(int)PlatformUtils.GetCurrentPlatform()];
			}
		}

		public ColorGradingModel colorGrading
		{
			get
			{
				return colorGradingModels[(int)PlatformUtils.GetCurrentPlatform()];
			}
		}

		public UserLutModel userLut
		{
			get
			{
				return userLutModels[(int)PlatformUtils.GetCurrentPlatform()];
			}
		}

		public ChromaticAberrationModel chromaticAberration
		{
			get
			{
				return chromaticAberrationModels[(int)PlatformUtils.GetCurrentPlatform()];
			}
		}

		public GrainModel grain
		{
			get
			{
				return grainModels[(int)PlatformUtils.GetCurrentPlatform()];
			}
		}

		public VignetteModel vignette
		{
			get
			{
				return vignetteModels[(int)PlatformUtils.GetCurrentPlatform()];
			}
		}

		public DitheringModel dithering
		{
			get
			{
				return ditheringModels[(int)PlatformUtils.GetCurrentPlatform()];
			}
		}

		public TiltShiftModel tiltshift
		{
			get
			{
				return tiltshiftModels[(int)PlatformUtils.GetCurrentPlatform()];
			}
		}
	}
}
