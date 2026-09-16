using System.Collections.Generic;

namespace UnityEngine.PostProcessing
{
	public static class PostProcessingBehaviourUtils
	{
		public enum LerpFilter
		{
			AmbientOcclusion = 0,
			Antialiasing = 1,
			Bloom = 2,
			ChromaticAberration = 3,
			ColorGrading = 4,
			DepthOfField = 5,
			Dithering = 6,
			EyeAdaptation = 7,
			Fog = 8,
			Grain = 9,
			MotionBlur = 10,
			ScreenSpaceReflection = 11,
			TiltShift = 12,
			UserLut = 13,
			Vignette = 14
		}

		public enum ValuesToLerp
		{
			EnabledState = 0,
			Parameters = 1
		}

		private abstract class Lerper
		{
			protected enum LerpType
			{
				OnToOn = 0,
				OnToOff = 1,
				OffToOff = 2,
				OffToOn = 3
			}

			public abstract LerpFilter LerpTarget { get; }

			public void Lerp(PostProcessingModel _start, PostProcessingModel _end, ref PostProcessingModel _delta, float _progress, int _valuesToLerp)
			{
				LerpType lerpType = GetLerpType(_start, _end, _delta, _valuesToLerp);
				if (lerpType == LerpType.OnToOff)
				{
					_delta.enabled = !Mathf.Approximately(_progress, 1f);
				}
				if (lerpType == LerpType.OffToOn)
				{
					_delta.enabled = !Mathf.Approximately(_progress, 0f);
				}
				if (lerpType != LerpType.OffToOff && MaskUtils.HasFlag(_valuesToLerp, ValuesToLerp.Parameters))
				{
					LerpValues((lerpType != LerpType.OffToOn) ? _start : CreateOffState(_end), (lerpType != LerpType.OnToOff) ? _end : CreateOffState(_start), ref _delta, _progress, lerpType);
				}
			}

			protected abstract void LerpValues(PostProcessingModel _start, PostProcessingModel _end, ref PostProcessingModel _delta, float _progress, LerpType _type);

			public abstract PostProcessingModel GetRequiredModel(PostProcessingProfile _profile);

			private LerpType GetLerpType(PostProcessingModel _start, PostProcessingModel _end, PostProcessingModel _delta, int _valuesToLerp)
			{
				if (MaskUtils.HasFlag(_valuesToLerp, ValuesToLerp.EnabledState))
				{
					if (_start.enabled == _end.enabled)
					{
						return (!_start.enabled) ? LerpType.OffToOff : LerpType.OnToOn;
					}
					if (_start.enabled && !_end.enabled)
					{
						return LerpType.OnToOff;
					}
					return LerpType.OffToOn;
				}
				return (!_delta.enabled) ? LerpType.OffToOff : LerpType.OnToOn;
			}

			protected abstract PostProcessingModel CreateOffState(PostProcessingModel _state);
		}

		private class AmbientOcclusionLerper : Lerper
		{
			public override LerpFilter LerpTarget
			{
				get
				{
					return LerpFilter.AmbientOcclusion;
				}
			}

			protected override void LerpValues(PostProcessingModel _start, PostProcessingModel _end, ref PostProcessingModel _delta, float _progress, LerpType _type)
			{
				AmbientOcclusionModel.Settings settings = (_start as AmbientOcclusionModel).settings;
				AmbientOcclusionModel.Settings settings2 = (_end as AmbientOcclusionModel).settings;
				AmbientOcclusionModel.Settings settings3 = (_delta as AmbientOcclusionModel).settings;
				settings3.blurSize = Mathf.Lerp(settings.blurSize, settings2.blurSize, _progress);
				settings3.distanceFalloff = Mathf.Lerp(settings.distanceFalloff, settings2.distanceFalloff, _progress);
				settings3.intensity = Mathf.Lerp(settings.intensity, settings2.intensity, _progress);
				settings3.radius = Mathf.Lerp(settings.radius, settings2.radius, _progress);
				settings3.downsampling = ((!(_progress < 0.5f)) ? settings2.downsampling : settings.downsampling);
				settings3.RotationTexture = ((!(_progress < 0.5f)) ? settings2.RotationTexture : settings.RotationTexture);
				AmbientOcclusionModel.SampleCount[] array = new AmbientOcclusionModel.SampleCount[4]
				{
					AmbientOcclusionModel.SampleCount.Lowest,
					AmbientOcclusionModel.SampleCount.Low,
					AmbientOcclusionModel.SampleCount.Medium,
					AmbientOcclusionModel.SampleCount.High
				};
				int valueArrayIndex = GetValueArrayIndex(array, settings.sampleCount);
				int valueArrayIndex2 = GetValueArrayIndex(array, settings2.sampleCount);
				settings3.sampleCount = array[(int)Mathf.Lerp(valueArrayIndex, valueArrayIndex2, _progress)];
				(_delta as AmbientOcclusionModel).settings = settings3;
			}

			public override PostProcessingModel GetRequiredModel(PostProcessingProfile _profile)
			{
				return _profile.ambientOcclusion;
			}

			protected override PostProcessingModel CreateOffState(PostProcessingModel _state)
			{
				AmbientOcclusionModel ambientOcclusionModel = new AmbientOcclusionModel();
				AmbientOcclusionModel.Settings settings = (_state as AmbientOcclusionModel).settings;
				settings.intensity = 0f;
				ambientOcclusionModel.settings = settings;
				return ambientOcclusionModel;
			}
		}

		private class AntialiasingLerper : Lerper
		{
			public override LerpFilter LerpTarget
			{
				get
				{
					return LerpFilter.Antialiasing;
				}
			}

			protected override void LerpValues(PostProcessingModel _start, PostProcessingModel _end, ref PostProcessingModel _delta, float _progress, LerpType _type)
			{
				AntialiasingModel.Settings settings = (_start as AntialiasingModel).settings;
				AntialiasingModel.Settings settings2 = (_end as AntialiasingModel).settings;
				AntialiasingModel.Settings settings3 = (_delta as AntialiasingModel).settings;
				if (settings.method == settings2.method)
				{
					settings3.method = settings2.method;
					switch (settings3.method)
					{
					case AntialiasingModel.Method.Fxaa:
					{
						AntialiasingModel.FxaaSettings fxaaSettings = ((!(_progress < 0.5f)) ? settings2.fxaaSettings : settings.fxaaSettings);
						settings3.fxaaSettings.preset = fxaaSettings.preset;
						break;
					}
					case AntialiasingModel.Method.Taa:
					{
						AntialiasingModel.TaaSettings taaSettings = ((!(_progress < 0.5f)) ? settings2.taaSettings : settings.taaSettings);
						settings3.taaSettings.jitterSpread = Mathf.Lerp(settings.taaSettings.jitterSpread, settings2.taaSettings.jitterSpread, _progress);
						settings3.taaSettings.motionBlending = Mathf.Lerp(settings.taaSettings.motionBlending, settings2.taaSettings.motionBlending, _progress);
						settings3.taaSettings.sharpen = Mathf.Lerp(settings.taaSettings.sharpen, settings2.taaSettings.sharpen, _progress);
						settings3.taaSettings.stationaryBlending = Mathf.Lerp(settings.taaSettings.stationaryBlending, settings2.taaSettings.stationaryBlending, _progress);
						break;
					}
					}
				}
				else
				{
					settings3.method = ((!(_progress < 0.5f)) ? settings2.method : settings.method);
					switch (settings3.method)
					{
					case AntialiasingModel.Method.Fxaa:
					{
						AntialiasingModel.FxaaSettings fxaaSettings2 = ((!(_progress < 0.5f)) ? settings2.fxaaSettings : settings.fxaaSettings);
						settings3.fxaaSettings.preset = fxaaSettings2.preset;
						break;
					}
					case AntialiasingModel.Method.Taa:
					{
						AntialiasingModel.TaaSettings taaSettings2 = ((!(_progress < 0.5f)) ? settings2.taaSettings : settings.taaSettings);
						settings3.taaSettings.jitterSpread = taaSettings2.jitterSpread;
						settings3.taaSettings.motionBlending = taaSettings2.motionBlending;
						settings3.taaSettings.sharpen = taaSettings2.sharpen;
						settings3.taaSettings.stationaryBlending = taaSettings2.stationaryBlending;
						break;
					}
					}
				}
				(_delta as AntialiasingModel).settings = settings3;
			}

			public override PostProcessingModel GetRequiredModel(PostProcessingProfile _profile)
			{
				return _profile.antialiasing;
			}

			protected override PostProcessingModel CreateOffState(PostProcessingModel _state)
			{
				AntialiasingModel antialiasingModel = new AntialiasingModel();
				AntialiasingModel.Settings settings = (_state as AntialiasingModel).settings;
				antialiasingModel.settings = settings;
				return antialiasingModel;
			}
		}

		private class BloomLerper : Lerper
		{
			public override LerpFilter LerpTarget
			{
				get
				{
					return LerpFilter.Bloom;
				}
			}

			protected override void LerpValues(PostProcessingModel _start, PostProcessingModel _end, ref PostProcessingModel _delta, float _progress, LerpType _type)
			{
				BloomModel.Settings settings = (_start as BloomModel).settings;
				BloomModel.Settings settings2 = (_end as BloomModel).settings;
				BloomModel.Settings settings3 = (_delta as BloomModel).settings;
				settings3.bloom.antiFlicker = ((!(_progress < 0.5f)) ? settings2.bloom.antiFlicker : settings.bloom.antiFlicker);
				settings3.bloom.intensity = Mathf.Lerp(settings.bloom.intensity, settings2.bloom.intensity, _progress);
				settings3.bloom.radius = Mathf.Lerp(settings.bloom.radius, settings2.bloom.radius, _progress);
				settings3.bloom.softKnee = Mathf.Lerp(settings.bloom.softKnee, settings2.bloom.softKnee, _progress);
				settings3.bloom.threshold = Mathf.Lerp(settings.bloom.threshold, settings2.bloom.threshold, _progress);
				settings3.bloom.thresholdLinear = Mathf.Lerp(settings.bloom.thresholdLinear, settings2.bloom.thresholdLinear, _progress);
				settings3.lensDirt.intensity = Mathf.Lerp(settings.lensDirt.intensity, settings2.lensDirt.intensity, _progress);
				settings3.lensDirt.texture = ((!(_progress < 0.5f)) ? settings2.lensDirt.texture : settings.lensDirt.texture);
				(_delta as BloomModel).settings = settings3;
			}

			public override PostProcessingModel GetRequiredModel(PostProcessingProfile _profile)
			{
				return _profile.bloom;
			}

			protected override PostProcessingModel CreateOffState(PostProcessingModel _state)
			{
				BloomModel bloomModel = new BloomModel();
				BloomModel.Settings settings = (_state as BloomModel).settings;
				settings.bloom.intensity = 0f;
				settings.lensDirt.intensity = 0f;
				bloomModel.settings = settings;
				return bloomModel;
			}
		}

		private class ChromaticAberrationLerper : Lerper
		{
			public override LerpFilter LerpTarget
			{
				get
				{
					return LerpFilter.ChromaticAberration;
				}
			}

			protected override void LerpValues(PostProcessingModel _start, PostProcessingModel _end, ref PostProcessingModel _delta, float _progress, LerpType _type)
			{
				ChromaticAberrationModel.Settings settings = (_start as ChromaticAberrationModel).settings;
				ChromaticAberrationModel.Settings settings2 = (_end as ChromaticAberrationModel).settings;
				ChromaticAberrationModel.Settings settings3 = (_delta as ChromaticAberrationModel).settings;
				settings3.intensity = Mathf.Lerp(settings.intensity, settings2.intensity, _progress);
				settings3.spectralTexture = ((!(_progress < 0.5f)) ? settings2.spectralTexture : settings.spectralTexture);
				(_delta as ChromaticAberrationModel).settings = settings3;
			}

			public override PostProcessingModel GetRequiredModel(PostProcessingProfile _profile)
			{
				return _profile.chromaticAberration;
			}

			protected override PostProcessingModel CreateOffState(PostProcessingModel _state)
			{
				ChromaticAberrationModel chromaticAberrationModel = new ChromaticAberrationModel();
				ChromaticAberrationModel.Settings settings = (_state as ChromaticAberrationModel).settings;
				settings.intensity = 0f;
				chromaticAberrationModel.settings = settings;
				return chromaticAberrationModel;
			}
		}

		private class ColorGradingLerper : Lerper
		{
			public override LerpFilter LerpTarget
			{
				get
				{
					return LerpFilter.ColorGrading;
				}
			}

			protected override void LerpValues(PostProcessingModel _start, PostProcessingModel _end, ref PostProcessingModel _delta, float _progress, LerpType _type)
			{
				ColorGradingModel.Settings settings = (_start as ColorGradingModel).settings;
				ColorGradingModel.Settings settings2 = (_end as ColorGradingModel).settings;
				ColorGradingModel.Settings settings3 = (_delta as ColorGradingModel).settings;
				settings3.basic.contrast = Mathf.Lerp(settings.basic.contrast, settings2.basic.contrast, _progress);
				settings3.basic.hueShift = Mathf.Lerp(settings.basic.hueShift, settings2.basic.hueShift, _progress);
				settings3.basic.postExposure = Mathf.Lerp(settings.basic.postExposure, settings2.basic.postExposure, _progress);
				settings3.basic.saturation = Mathf.Lerp(settings.basic.saturation, settings2.basic.saturation, _progress);
				settings3.basic.temperature = Mathf.Lerp(settings.basic.temperature, settings2.basic.temperature, _progress);
				settings3.basic.tint = Mathf.Lerp(settings.basic.tint, settings2.basic.tint, _progress);
				settings3.channelMixer.red = Vector3.Lerp(settings.channelMixer.red, settings2.channelMixer.red, _progress);
				settings3.channelMixer.green = Vector3.Lerp(settings.channelMixer.green, settings2.channelMixer.green, _progress);
				settings3.channelMixer.blue = Vector3.Lerp(settings.channelMixer.blue, settings2.channelMixer.blue, _progress);
				if (settings.colorWheels.mode == settings2.colorWheels.mode)
				{
					settings3.colorWheels.mode = settings2.colorWheels.mode;
					switch (settings3.colorWheels.mode)
					{
					case ColorGradingModel.ColorWheelMode.Linear:
						settings3.colorWheels.linear.gain = Color.Lerp(settings.colorWheels.linear.gain, settings2.colorWheels.linear.gain, _progress);
						settings3.colorWheels.linear.gamma = Color.Lerp(settings.colorWheels.linear.gamma, settings2.colorWheels.linear.gamma, _progress);
						settings3.colorWheels.linear.lift = Color.Lerp(settings.colorWheels.linear.lift, settings2.colorWheels.linear.lift, _progress);
						break;
					case ColorGradingModel.ColorWheelMode.Log:
						settings3.colorWheels.log.offset = Color.Lerp(settings.colorWheels.log.offset, settings2.colorWheels.log.offset, _progress);
						settings3.colorWheels.log.power = Color.Lerp(settings.colorWheels.log.power, settings2.colorWheels.log.power, _progress);
						settings3.colorWheels.log.slope = Color.Lerp(settings.colorWheels.log.slope, settings2.colorWheels.log.slope, _progress);
						break;
					}
				}
				else
				{
					settings3.colorWheels.mode = ((!(_progress < 0.5f)) ? settings2.colorWheels.mode : settings.colorWheels.mode);
					switch (settings3.colorWheels.mode)
					{
					case ColorGradingModel.ColorWheelMode.Linear:
					{
						ColorGradingModel.LinearWheelsSettings linearWheelsSettings = ((!(_progress < 0.5f)) ? settings2.colorWheels.linear : settings.colorWheels.linear);
						settings3.colorWheels.linear.gain = linearWheelsSettings.gain;
						settings3.colorWheels.linear.gamma = linearWheelsSettings.gamma;
						settings3.colorWheels.linear.lift = linearWheelsSettings.lift;
						break;
					}
					case ColorGradingModel.ColorWheelMode.Log:
					{
						ColorGradingModel.LogWheelsSettings logWheelsSettings = ((!(_progress < 0.5f)) ? settings2.colorWheels.log : settings.colorWheels.log);
						settings3.colorWheels.log.offset = logWheelsSettings.offset;
						settings3.colorWheels.log.power = logWheelsSettings.power;
						settings3.colorWheels.log.slope = logWheelsSettings.slope;
						break;
					}
					}
				}
				settings3.curves.red = ((!(_progress < 0.5f)) ? settings2.curves.red : settings.curves.red);
				settings3.curves.green = ((!(_progress < 0.5f)) ? settings2.curves.green : settings.curves.green);
				settings3.curves.blue = ((!(_progress < 0.5f)) ? settings2.curves.blue : settings.curves.blue);
				settings3.curves.hueVShue = ((!(_progress < 0.5f)) ? settings2.curves.hueVShue : settings.curves.hueVShue);
				settings3.curves.hueVSsat = ((!(_progress < 0.5f)) ? settings2.curves.hueVSsat : settings.curves.hueVSsat);
				settings3.curves.lumVSsat = ((!(_progress < 0.5f)) ? settings2.curves.lumVSsat : settings.curves.lumVSsat);
				settings3.curves.master = ((!(_progress < 0.5f)) ? settings2.curves.master : settings.curves.master);
				settings3.curves.satVSsat = ((!(_progress < 0.5f)) ? settings2.curves.satVSsat : settings.curves.satVSsat);
				settings3.tonemapping.tonemapper = ((!(_progress < 0.5f)) ? settings2.tonemapping.tonemapper : settings.tonemapping.tonemapper);
				settings3.tonemapping.neutralBlackIn = Mathf.Lerp(settings.tonemapping.neutralBlackIn, settings2.tonemapping.neutralBlackIn, _progress);
				settings3.tonemapping.neutralBlackOut = Mathf.Lerp(settings.tonemapping.neutralBlackOut, settings2.tonemapping.neutralBlackOut, _progress);
				settings3.tonemapping.neutralWhiteClip = Mathf.Lerp(settings.tonemapping.neutralWhiteClip, settings2.tonemapping.neutralWhiteClip, _progress);
				settings3.tonemapping.neutralWhiteIn = Mathf.Lerp(settings.tonemapping.neutralWhiteIn, settings2.tonemapping.neutralWhiteIn, _progress);
				settings3.tonemapping.neutralWhiteLevel = Mathf.Lerp(settings.tonemapping.neutralWhiteLevel, settings2.tonemapping.neutralWhiteLevel, _progress);
				settings3.tonemapping.neutralWhiteOut = Mathf.Lerp(settings.tonemapping.neutralWhiteOut, settings2.tonemapping.neutralWhiteOut, _progress);
				(_delta as ColorGradingModel).settings = settings3;
			}

			public override PostProcessingModel GetRequiredModel(PostProcessingProfile _profile)
			{
				return _profile.colorGrading;
			}

			protected override PostProcessingModel CreateOffState(PostProcessingModel _state)
			{
				ColorGradingModel colorGradingModel = new ColorGradingModel();
				ColorGradingModel.Settings settings = (_state as ColorGradingModel).settings;
				colorGradingModel.settings = settings;
				return colorGradingModel;
			}
		}

		private class DepthOfFieldLerper : Lerper
		{
			public override LerpFilter LerpTarget
			{
				get
				{
					return LerpFilter.DepthOfField;
				}
			}

			protected override void LerpValues(PostProcessingModel _start, PostProcessingModel _end, ref PostProcessingModel _delta, float _progress, LerpType _type)
			{
				DepthOfFieldModel.Settings settings = (_start as DepthOfFieldModel).settings;
				DepthOfFieldModel.Settings settings2 = (_end as DepthOfFieldModel).settings;
				DepthOfFieldModel.Settings settings3 = (_delta as DepthOfFieldModel).settings;
				settings3.useCameraFov = ((!(_progress < 0.5f)) ? settings2.useCameraFov : settings.useCameraFov);
				settings3.aperture = Mathf.Lerp(settings.aperture, settings2.aperture, _progress);
				settings3.focalLength = Mathf.Lerp(settings.focalLength, settings2.focalLength, _progress);
				settings3.focusDistance = Mathf.Lerp(settings.focusDistance, settings2.focusDistance, _progress);
				DepthOfFieldModel.KernelSize[] array = new DepthOfFieldModel.KernelSize[4]
				{
					DepthOfFieldModel.KernelSize.Small,
					DepthOfFieldModel.KernelSize.Medium,
					DepthOfFieldModel.KernelSize.Large,
					DepthOfFieldModel.KernelSize.VeryLarge
				};
				int valueArrayIndex = GetValueArrayIndex(array, settings.kernelSize);
				int valueArrayIndex2 = GetValueArrayIndex(array, settings2.kernelSize);
				settings3.kernelSize = array[(int)Mathf.Lerp(valueArrayIndex, valueArrayIndex2, _progress)];
				(_delta as DepthOfFieldModel).settings = settings3;
			}

			public override PostProcessingModel GetRequiredModel(PostProcessingProfile _profile)
			{
				return _profile.depthOfField;
			}

			protected override PostProcessingModel CreateOffState(PostProcessingModel _state)
			{
				DepthOfFieldModel depthOfFieldModel = new DepthOfFieldModel();
				DepthOfFieldModel.Settings settings = (_state as DepthOfFieldModel).settings;
				settings.focalLength = 1f;
				depthOfFieldModel.settings = settings;
				return depthOfFieldModel;
			}
		}

		private class DitheringLerper : Lerper
		{
			public override LerpFilter LerpTarget
			{
				get
				{
					return LerpFilter.Dithering;
				}
			}

			protected override void LerpValues(PostProcessingModel _start, PostProcessingModel _end, ref PostProcessingModel _delta, float _progress, LerpType _type)
			{
			}

			public override PostProcessingModel GetRequiredModel(PostProcessingProfile _profile)
			{
				return _profile.dithering;
			}

			protected override PostProcessingModel CreateOffState(PostProcessingModel _state)
			{
				DitheringModel ditheringModel = new DitheringModel();
				DitheringModel.Settings settings = (_state as DitheringModel).settings;
				ditheringModel.settings = settings;
				return ditheringModel;
			}
		}

		private class EyeAdaptionLerper : Lerper
		{
			public override LerpFilter LerpTarget
			{
				get
				{
					return LerpFilter.EyeAdaptation;
				}
			}

			protected override void LerpValues(PostProcessingModel _start, PostProcessingModel _end, ref PostProcessingModel _delta, float _progress, LerpType _type)
			{
				EyeAdaptationModel.Settings settings = (_start as EyeAdaptationModel).settings;
				EyeAdaptationModel.Settings settings2 = (_end as EyeAdaptationModel).settings;
				EyeAdaptationModel.Settings settings3 = (_delta as EyeAdaptationModel).settings;
				settings3.adaptationType = ((!(_progress < 0.5f)) ? settings2.adaptationType : settings.adaptationType);
				settings3.dynamicKeyValue = ((!(_progress < 0.5f)) ? settings2.dynamicKeyValue : settings.dynamicKeyValue);
				settings3.highPercent = Mathf.Lerp(settings.highPercent, settings2.highPercent, _progress);
				settings3.keyValue = Mathf.Lerp(settings.keyValue, settings2.keyValue, _progress);
				settings3.logMax = (int)Mathf.Lerp(settings.logMax, settings2.logMax, _progress);
				settings3.logMin = (int)Mathf.Lerp(settings.logMin, settings2.logMin, _progress);
				settings3.lowPercent = Mathf.Lerp(settings.lowPercent, settings2.lowPercent, _progress);
				settings3.maxLuminance = Mathf.Lerp(settings.maxLuminance, settings2.maxLuminance, _progress);
				settings3.minLuminance = Mathf.Lerp(settings.minLuminance, settings2.minLuminance, _progress);
				settings3.speedDown = Mathf.Lerp(settings.speedDown, settings2.speedDown, _progress);
				settings3.speedUp = Mathf.Lerp(settings.speedUp, settings2.speedUp, _progress);
				(_delta as EyeAdaptationModel).settings = settings3;
			}

			public override PostProcessingModel GetRequiredModel(PostProcessingProfile _profile)
			{
				return _profile.eyeAdaptation;
			}

			protected override PostProcessingModel CreateOffState(PostProcessingModel _state)
			{
				EyeAdaptationModel eyeAdaptationModel = new EyeAdaptationModel();
				EyeAdaptationModel.Settings settings = (_state as EyeAdaptationModel).settings;
				eyeAdaptationModel.settings = settings;
				return eyeAdaptationModel;
			}
		}

		private class FogLerper : Lerper
		{
			public override LerpFilter LerpTarget
			{
				get
				{
					return LerpFilter.Fog;
				}
			}

			protected override void LerpValues(PostProcessingModel _start, PostProcessingModel _end, ref PostProcessingModel _delta, float _progress, LerpType _type)
			{
				FogModel.Settings settings = (_start as FogModel).settings;
				FogModel.Settings settings2 = (_end as FogModel).settings;
				FogModel.Settings settings3 = (_delta as FogModel).settings;
				settings3.excludeSkybox = ((!(_progress < 0.5f)) ? settings2.excludeSkybox : settings.excludeSkybox);
				(_delta as FogModel).settings = settings3;
			}

			public override PostProcessingModel GetRequiredModel(PostProcessingProfile _profile)
			{
				return _profile.fog;
			}

			protected override PostProcessingModel CreateOffState(PostProcessingModel _state)
			{
				FogModel fogModel = new FogModel();
				FogModel.Settings settings = (_state as FogModel).settings;
				fogModel.settings = settings;
				return fogModel;
			}
		}

		private class GrainLerper : Lerper
		{
			public override LerpFilter LerpTarget
			{
				get
				{
					return LerpFilter.Grain;
				}
			}

			protected override void LerpValues(PostProcessingModel _start, PostProcessingModel _end, ref PostProcessingModel _delta, float _progress, LerpType _type)
			{
				GrainModel.Settings settings = (_start as GrainModel).settings;
				GrainModel.Settings settings2 = (_end as GrainModel).settings;
				GrainModel.Settings settings3 = (_delta as GrainModel).settings;
				settings3.colored = ((!(_progress < 0.5f)) ? settings2.colored : settings.colored);
				settings3.intensity = Mathf.Lerp(settings.intensity, settings2.intensity, _progress);
				settings3.luminanceContribution = Mathf.Lerp(settings.luminanceContribution, settings2.luminanceContribution, _progress);
				settings3.size = Mathf.Lerp(settings.size, settings2.size, _progress);
				(_delta as GrainModel).settings = settings3;
			}

			public override PostProcessingModel GetRequiredModel(PostProcessingProfile _profile)
			{
				return _profile.grain;
			}

			protected override PostProcessingModel CreateOffState(PostProcessingModel _state)
			{
				GrainModel grainModel = new GrainModel();
				GrainModel.Settings settings = (_state as GrainModel).settings;
				settings.intensity = 0f;
				grainModel.settings = settings;
				return grainModel;
			}
		}

		private class MotionBlurLerper : Lerper
		{
			public override LerpFilter LerpTarget
			{
				get
				{
					return LerpFilter.MotionBlur;
				}
			}

			protected override void LerpValues(PostProcessingModel _start, PostProcessingModel _end, ref PostProcessingModel _delta, float _progress, LerpType _type)
			{
				MotionBlurModel.Settings settings = (_start as MotionBlurModel).settings;
				MotionBlurModel.Settings settings2 = (_end as MotionBlurModel).settings;
				MotionBlurModel.Settings settings3 = (_delta as MotionBlurModel).settings;
				settings3.frameBlending = ((!(_progress < 0.5f)) ? settings2.frameBlending : settings.frameBlending);
				settings3.sampleCount = (int)Mathf.Lerp(settings.sampleCount, settings2.sampleCount, _progress);
				settings3.shutterAngle = Mathf.Lerp(settings.shutterAngle, settings2.shutterAngle, _progress);
				(_delta as MotionBlurModel).settings = settings3;
			}

			public override PostProcessingModel GetRequiredModel(PostProcessingProfile _profile)
			{
				return _profile.motionBlur;
			}

			protected override PostProcessingModel CreateOffState(PostProcessingModel _state)
			{
				MotionBlurModel motionBlurModel = new MotionBlurModel();
				MotionBlurModel.Settings settings = (_state as MotionBlurModel).settings;
				settings.frameBlending = 0f;
				motionBlurModel.settings = settings;
				return motionBlurModel;
			}
		}

		private class ScreenSpaceReflectionLerper : Lerper
		{
			public override LerpFilter LerpTarget
			{
				get
				{
					return LerpFilter.ScreenSpaceReflection;
				}
			}

			protected override void LerpValues(PostProcessingModel _start, PostProcessingModel _end, ref PostProcessingModel _delta, float _progress, LerpType _type)
			{
				ScreenSpaceReflectionModel.Settings settings = (_start as ScreenSpaceReflectionModel).settings;
				ScreenSpaceReflectionModel.Settings settings2 = (_end as ScreenSpaceReflectionModel).settings;
				ScreenSpaceReflectionModel.Settings settings3 = (_delta as ScreenSpaceReflectionModel).settings;
				settings3.intensity.fadeDistance = Mathf.Lerp(settings.intensity.fadeDistance, settings2.intensity.fadeDistance, _progress);
				settings3.intensity.fresnelFade = Mathf.Lerp(settings.intensity.fresnelFade, settings2.intensity.fresnelFade, _progress);
				settings3.intensity.fresnelFadePower = Mathf.Lerp(settings.intensity.fresnelFadePower, settings2.intensity.fresnelFadePower, _progress);
				settings3.intensity.reflectionMultiplier = Mathf.Lerp(settings.intensity.reflectionMultiplier, settings2.intensity.reflectionMultiplier, _progress);
				settings3.reflection.blendType = ((!(_progress < 0.5f)) ? settings2.reflection.blendType : settings.reflection.blendType);
				settings3.reflection.iterationCount = (int)Mathf.Lerp(settings.reflection.iterationCount, settings2.reflection.iterationCount, _progress);
				settings3.reflection.maxDistance = Mathf.Lerp(settings.reflection.maxDistance, settings2.reflection.maxDistance, _progress);
				settings3.reflection.reflectBackfaces = ((!(_progress < 0.5f)) ? settings2.reflection.reflectBackfaces : settings.reflection.reflectBackfaces);
				settings3.reflection.reflectionBlur = Mathf.Lerp(settings.reflection.reflectionBlur, settings2.reflection.reflectionBlur, _progress);
				ScreenSpaceReflectionModel.SSRResolution[] array = new ScreenSpaceReflectionModel.SSRResolution[2]
				{
					ScreenSpaceReflectionModel.SSRResolution.Low,
					ScreenSpaceReflectionModel.SSRResolution.High
				};
				int valueArrayIndex = GetValueArrayIndex(array, settings.reflection.reflectionQuality);
				int valueArrayIndex2 = GetValueArrayIndex(array, settings2.reflection.reflectionQuality);
				settings3.reflection.reflectionQuality = array[(int)Mathf.Lerp(valueArrayIndex, valueArrayIndex2, _progress)];
				settings3.reflection.stepSize = (int)Mathf.Lerp(settings.reflection.stepSize, settings2.reflection.stepSize, _progress);
				settings3.reflection.widthModifier = Mathf.Lerp(settings.reflection.widthModifier, settings2.reflection.widthModifier, _progress);
				settings3.screenEdgeMask.intensity = Mathf.Lerp(settings.screenEdgeMask.intensity, settings2.screenEdgeMask.intensity, _progress);
				(_delta as ScreenSpaceReflectionModel).settings = settings3;
			}

			public override PostProcessingModel GetRequiredModel(PostProcessingProfile _profile)
			{
				return _profile.screenSpaceReflection;
			}

			protected override PostProcessingModel CreateOffState(PostProcessingModel _state)
			{
				ScreenSpaceReflectionModel screenSpaceReflectionModel = new ScreenSpaceReflectionModel();
				ScreenSpaceReflectionModel.Settings settings = (_state as ScreenSpaceReflectionModel).settings;
				settings.intensity.reflectionMultiplier = 0f;
				screenSpaceReflectionModel.settings = settings;
				return screenSpaceReflectionModel;
			}
		}

		private class TiltShiftLerper : Lerper
		{
			public override LerpFilter LerpTarget
			{
				get
				{
					return LerpFilter.TiltShift;
				}
			}

			protected override void LerpValues(PostProcessingModel _start, PostProcessingModel _end, ref PostProcessingModel _delta, float _progress, LerpType _type)
			{
				TiltShiftModel.Settings settings = (_start as TiltShiftModel).settings;
				TiltShiftModel.Settings settings2 = (_end as TiltShiftModel).settings;
				TiltShiftModel.Settings settings3 = (_delta as TiltShiftModel).settings;
				settings3.BlurArea = Mathf.Lerp(settings.BlurArea, settings2.BlurArea, _progress);
				settings3.Downsample = ((!(_progress < 0.5f)) ? settings2.Downsample : settings.Downsample);
				settings3.Iterations = (int)Mathf.Lerp(settings.Iterations, settings2.Iterations, _progress);
				(_delta as TiltShiftModel).settings = settings3;
			}

			public override PostProcessingModel GetRequiredModel(PostProcessingProfile _profile)
			{
				return _profile.tiltshift;
			}

			protected override PostProcessingModel CreateOffState(PostProcessingModel _state)
			{
				TiltShiftModel tiltShiftModel = new TiltShiftModel();
				TiltShiftModel.Settings settings = (_state as TiltShiftModel).settings;
				settings.Iterations = 0;
				tiltShiftModel.settings = settings;
				return tiltShiftModel;
			}
		}

		private class UserLutLerper : Lerper
		{
			private Material mat;

			public override LerpFilter LerpTarget
			{
				get
				{
					return LerpFilter.UserLut;
				}
			}

			protected override void LerpValues(PostProcessingModel _start, PostProcessingModel _end, ref PostProcessingModel _delta, float _progress, LerpType _type)
			{
				UserLutModel.Settings settings = (_start as UserLutModel).settings;
				UserLutModel.Settings settings2 = (_end as UserLutModel).settings;
				UserLutModel.Settings settings3 = (_delta as UserLutModel).settings;
				settings3.contribution = Mathf.Lerp(settings.contribution, settings2.contribution, _progress);
				settings3.lut = ((!(_progress < 0.5f)) ? settings2.lut : settings.lut);
				(_delta as UserLutModel).settings = settings3;
			}

			public override PostProcessingModel GetRequiredModel(PostProcessingProfile _profile)
			{
				return _profile.userLut;
			}

			protected override PostProcessingModel CreateOffState(PostProcessingModel _state)
			{
				UserLutModel userLutModel = new UserLutModel();
				UserLutModel.Settings settings = (_state as UserLutModel).settings;
				settings.contribution = 0f;
				userLutModel.settings = settings;
				return userLutModel;
			}
		}

		private class VignetteLerper : Lerper
		{
			public override LerpFilter LerpTarget
			{
				get
				{
					return LerpFilter.Vignette;
				}
			}

			protected override void LerpValues(PostProcessingModel _start, PostProcessingModel _end, ref PostProcessingModel _delta, float _progress, LerpType _type)
			{
				VignetteModel.Settings settings = (_start as VignetteModel).settings;
				VignetteModel.Settings settings2 = (_end as VignetteModel).settings;
				VignetteModel.Settings settings3 = (_delta as VignetteModel).settings;
				settings3.center = Vector3.Lerp(settings.center, settings2.center, _progress);
				settings3.color = Color.Lerp(settings.color, settings2.color, _progress);
				settings3.intensity = Mathf.Lerp(settings.intensity, settings2.intensity, _progress);
				settings3.mask = ((!(_progress < 0.5f)) ? settings2.mask : settings.mask);
				settings3.mode = ((!(_progress < 0.5f)) ? settings2.mode : settings.mode);
				settings3.opacity = Mathf.Lerp(settings.opacity, settings2.opacity, _progress);
				settings3.rounded = ((!(_progress < 0.5f)) ? settings2.rounded : settings.rounded);
				settings3.roundness = Mathf.Lerp(settings.roundness, settings2.roundness, _progress);
				settings3.smoothness = Mathf.Lerp(settings.smoothness, settings2.smoothness, _progress);
				(_delta as VignetteModel).settings = settings3;
			}

			public override PostProcessingModel GetRequiredModel(PostProcessingProfile _profile)
			{
				return _profile.vignette;
			}

			protected override PostProcessingModel CreateOffState(PostProcessingModel _state)
			{
				VignetteModel vignetteModel = new VignetteModel();
				VignetteModel.Settings settings = (_state as VignetteModel).settings;
				settings.intensity = 0f;
				settings.opacity = 0f;
				vignetteModel.settings = settings;
				return vignetteModel;
			}
		}

		private static Lerper[] lerpers = new Lerper[15]
		{
			new AmbientOcclusionLerper(),
			new AntialiasingLerper(),
			new BloomLerper(),
			new ChromaticAberrationLerper(),
			new ColorGradingLerper(),
			new DepthOfFieldLerper(),
			new DitheringLerper(),
			new EyeAdaptionLerper(),
			new FogLerper(),
			new GrainLerper(),
			new MotionBlurLerper(),
			new ScreenSpaceReflectionLerper(),
			new TiltShiftLerper(),
			new UserLutLerper(),
			new VignetteLerper()
		};

		public static void Lerp(PostProcessingProfile _start, PostProcessingProfile _end, ref PostProcessingProfile _delta, float _progress, int _filter = int.MaxValue, int _valuesToLerp = int.MaxValue)
		{
			for (int i = 0; i < lerpers.Length; i++)
			{
				Lerper lerper = lerpers[i];
				if (MaskUtils.HasFlag(_filter, lerper.LerpTarget))
				{
					PostProcessingModel _delta2 = lerper.GetRequiredModel(_delta);
					lerper.Lerp(lerper.GetRequiredModel(_start), lerper.GetRequiredModel(_end), ref _delta2, _progress, _valuesToLerp);
				}
			}
		}

		private static int GetValueArrayIndex<T>(T[] _array, T _value)
		{
			for (int i = 0; i < _array.Length; i++)
			{
				if (EqualityComparer<T>.Default.Equals(_array[i], _value))
				{
					return i;
				}
			}
			return -1;
		}
	}
}
