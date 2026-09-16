using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class PCSaveManager : SaveManagerBase
{
	protected enum Type
	{
		Meta = 0,
		CoopSlot = 1
	}

	protected const string c_filename = "SaveFile";

	protected const string c_extension = ".save";

	private const int c_metaSaveSlot = -1;

	protected void BootstrapAwake()
	{
		StartCoroutine(LoadProfile(null));
	}

	public override IEnumerator<SaveLoadResult?> LoadProfile(GamepadUser _user)
	{
		DestroyMetaSession();
		CreateMetaSession();
		MetaGameProgress metaGameProgess = GetMetaGameProgress();
		ReturnValue<bool> hasSave = new ReturnValue<bool>();
		string fileAddress = GetFileAddress(Type.Meta, -1, -1);
		if (File.Exists(fileAddress))
		{
			byte[] bytes = File.ReadAllBytes(fileAddress);
			if (!metaGameProgess.ByteLoad(bytes))
			{
				SaveLoadResult? dialogOutcome = null;
				PlatformCallback callback = delegate(SaveLoadResult _result)
				{
					dialogOutcome = _result;
				};
				ShowCorruptedMetaDialog(callback);
				while (!dialogOutcome.HasValue)
				{
					yield return null;
				}
				yield return dialogOutcome;
			}
			else
			{
				yield return SaveLoadResult.Exists;
			}
		}
		else
		{
			yield return SaveLoadResult.NotExist;
		}
	}

	protected override IEnumerator<SaveLoadResult?> PlatformLoadSave(SaveMode _mode, int _slot, int _dlcNumber)
	{
		yield return LoadData(_mode, _slot, _dlcNumber);
	}

	private SaveLoadResult LoadData(SaveMode _mode, int _slot, int _dlcNumber)
	{
		ShowLoadIcon();
		GameSession gameSession = GameUtils.GetGameSession();
		string fileAddress = GetFileAddress(ModeToType(_mode), _slot, _dlcNumber);
		if (File.Exists(fileAddress))
		{
			byte[] array = File.ReadAllBytes(fileAddress);
			if (array == null || !gameSession.Progress.Load(array))
			{
				HideLoadIcon();
				return SaveLoadResult.Corrupted;
			}
			HideLoadIcon();
			return SaveLoadResult.Exists;
		}
		HideLoadIcon();
		return SaveLoadResult.NotExist;
	}

	protected override void PlatformSaveMetaProgress(PlatformCallback _callback)
	{
		SaveLoadResult result = SaveFile(GetMetaGameProgress(), Type.Meta, -1, -1);
		_callback(result);
	}

	protected override void PlatformSaveData(SaveMode _mode, int _slot, int _dlcNumber, PlatformCallback _callback)
	{
		SaveLoadResult result = SaveLoadResult.Exists;
		GameSession gameSession = GameUtils.GetGameSession();
		if (_mode == SaveMode.Main)
		{
			result = SaveFile(gameSession.Progress.SaveableData, ModeToType(_mode), _slot, _dlcNumber);
		}
		_callback(result);
	}

	private SaveLoadResult SaveFile(IByteSerialization _data, Type _type, int _slot, int _dlcNumber)
	{
		ShowSaveIcon();
		if (_data != null)
		{
			FileStream fileStream = null;
			try
			{
				string fileAddress = GetFileAddress(_type, _slot, _dlcNumber);
				string path = fileAddress.Substring(0, fileAddress.LastIndexOf('/'));
				if (!Directory.Exists(path))
				{
					Directory.CreateDirectory(path);
				}
				byte[] array = _data.ByteSave();
				fileStream = new FileStream(fileAddress, FileMode.OpenOrCreate, FileAccess.Write);
				fileStream.SetLength(array.Length);
				fileStream.Write(array, 0, array.Length);
				fileStream.Close();
			}
			catch (Exception ex)
			{
				if (fileStream != null)
				{
					fileStream.Close();
				}
				int num = ex.HResultPublic();
				if (num == -2147024857 || num == -2147024784)
				{
					HideSaveIcon();
					return SaveLoadResult.NoSpace;
				}
				HideSaveIcon();
				throw ex;
			}
		}
		HideSaveIcon();
		return SaveLoadResult.Exists;
	}

	protected IEnumerator HasXFile(string _fileName, ReturnValue<SaveLoadResult> _result)
	{
		bool flag = File.Exists(_fileName);
		_result.Value = ((!flag) ? SaveLoadResult.NotExist : SaveLoadResult.Exists);
		yield break;
	}

	public override IEnumerator HasMetaSaveFile(ReturnValue<SaveLoadResult> _result)
	{
		string path = GetFileAddress(Type.Meta, -1, -1);
		IEnumerator hasFile = HasXFile(path, _result);
		while (hasFile.MoveNext())
		{
			yield return null;
		}
		if (_result.Value == SaveLoadResult.Exists)
		{
			byte[] array = File.ReadAllBytes(path);
			if (array == null || !MetaGameProgress.Validate(array))
			{
				_result.Value = SaveLoadResult.Corrupted;
			}
		}
	}

	public override IEnumerator HasSaveFile(SaveMode _mode, int _slot, int _dlcNumber, ReturnValue<SaveLoadResult> _result)
	{
		string path = GetFileAddress(ModeToType(_mode), _slot, _dlcNumber);
		IEnumerator hasFile = HasXFile(path, _result);
		while (hasFile.MoveNext())
		{
			yield return null;
		}
		if (_result.Value == SaveLoadResult.Exists)
		{
			byte[] array = File.ReadAllBytes(path);
			if (array == null || !GameProgress.GameProgressData.Validate(array))
			{
				_result.Value = SaveLoadResult.Corrupted;
			}
		}
	}

	public override void DeleteSave(SaveMode _mode, int _slot, int _dlcNumber, CallbackVoid _callback = null)
	{
		File.Delete(GetFileAddress(ModeToType(_mode), _slot, _dlcNumber));
		if (_callback != null)
		{
			_callback();
		}
	}

	public override void DeleteMetaSave(CallbackVoid _callback = null)
	{
		File.Delete(GetFileAddress(Type.Meta, -1, -1));
		if (_callback != null)
		{
			_callback();
		}
	}

	protected virtual string GetSaveDirectory()
	{
		return Application.persistentDataPath + "/";
	}

	private string GetFileAddress(Type _type, int _slot, int _dlcNumber)
	{
		if (_dlcNumber != -1)
		{
			if (_dlcNumber > base.MaxDLC)
			{
			}
			_dlcNumber = Mathf.Clamp(_dlcNumber, 0, base.MaxDLC);
		}
		string text = _type.ToString() + "_SaveFile";
		if (_type != Type.Meta)
		{
			if (_dlcNumber != -1)
			{
				text = "DLC" + _dlcNumber + "_" + text;
			}
			text = text + "_" + _slot;
		}
		string saveDirectory = GetSaveDirectory();
		return saveDirectory + text + ".save";
	}

	private Type ModeToType(SaveMode _mode)
	{
		if (_mode == SaveMode.Main)
		{
			return Type.CoopSlot;
		}
		return Type.Meta;
	}
}
