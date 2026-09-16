using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class SaveManagerBase : Manager, ISaveManager
{
	protected delegate void PlatformCallback(SaveLoadResult _result);

	[SerializeField]
	public int m_maxSlots = 3;

	private bool m_savingMeta;

	private bool m_savingProgress;

	private List<GenericVoid> m_idleCallbacks = new List<GenericVoid>(2);

	[SerializeField]
	private GameObject m_metaSessionPrefab;

	private GameObject m_metaSession;

	private MetaGameProgress m_metaGameProgress;

	private IEnumerator m_deleteAllCo;

	private Suppressor m_showSave;

	private Suppressor m_showLoad;

	private T17DialogBox m_spinner;

	private T17DialogBox m_activeInputDialog;

	private T17DialogBox.DialogEvent m_activeInputHideCallback;

	protected int MaxSlots
	{
		get
		{
			return m_maxSlots;
		}
	}

	protected int MaxDLC
	{
		get
		{
			return DLCManagerBase.SupportedDLCLimit;
		}
	}

	public bool ProfileLoaded
	{
		get
		{
			return m_metaGameProgress != null;
		}
	}

	public bool IsSavingMeta
	{
		get
		{
			return m_savingMeta;
		}
	}

	public bool IsSavingProgress
	{
		get
		{
			return m_savingProgress;
		}
	}

	public bool IsSaving
	{
		get
		{
			return IsSavingMeta || IsSavingProgress;
		}
	}

	protected virtual void Awake()
	{
		m_activeInputHideCallback = delegate
		{
			m_activeInputDialog = null;
		};
	}

	public MetaGameProgress GetMetaGameProgress()
	{
		return m_metaGameProgress;
	}

	public void CreateMetaSession()
	{
		m_metaSession = m_metaSessionPrefab.InstantiateOnParent(null);
		m_metaGameProgress = m_metaSession.RequireComponentRecursive<MetaGameProgress>();
		m_metaGameProgress.ConsoleReset();
	}

	public virtual void UnloadProfile()
	{
		DestroyMetaSession();
	}

	public void DestroyMetaSession()
	{
		if (m_metaSession != null)
		{
			if (IsSaving)
			{
			}
			m_metaSession.DestroyImmediate();
			m_metaSession = null;
			m_metaGameProgress = null;
		}
	}

	protected void DestroySession()
	{
		GameSession gameSession = GameUtils.GetGameSession();
		if (gameSession != null)
		{
			gameSession.gameObject.DestroyImmediate();
		}
	}

	public virtual void DeleteAll()
	{
		DeleteMetaSave();
		DestroyMetaSession();
		CreateMetaSession();
		for (int i = 0; i < MaxSlots; i++)
		{
			DeleteSave(SaveMode.Main, i, -1);
			for (int j = 0; j < MaxDLC; j++)
			{
				DeleteSave(SaveMode.Main, i, j);
			}
		}
		DestroySession();
	}

	protected virtual void Update()
	{
		if (m_deleteAllCo != null && !m_deleteAllCo.MoveNext())
		{
			m_deleteAllCo = null;
		}
	}

	protected void ShowSaveIcon(bool _resetIconStartTime = true)
	{
		if (m_showSave == null)
		{
			m_showSave = SpinnerIconManager.Instance.Show(SpinnerIconManager.SpinnerIconType.Save, this, _resetIconStartTime);
		}
	}

	protected void HideSaveIcon()
	{
		if (m_showSave != null)
		{
			m_showSave.Release();
			m_showSave = null;
		}
	}

	protected void ShowLoadIcon(bool _resetIconStartTime = true)
	{
		if (m_showLoad == null)
		{
			m_showLoad = SpinnerIconManager.Instance.Show(SpinnerIconManager.SpinnerIconType.Load, this, _resetIconStartTime);
		}
	}

	protected void HideLoadIcon()
	{
		if (m_showLoad != null)
		{
			m_showLoad.Release();
			m_showLoad = null;
		}
	}

	public abstract IEnumerator<SaveLoadResult?> LoadProfile(GamepadUser _user);

	protected abstract IEnumerator<SaveLoadResult?> PlatformLoadSave(SaveMode _mode, int _slot, int _dlcNumber);

	public IEnumerator<SaveLoadResult?> LoadSave(SaveMode _mode, int _slot, int _dlcNumber)
	{
		IEnumerator<SaveLoadResult?> load = PlatformLoadSave(_mode, _slot, _dlcNumber);
		while (load.MoveNext())
		{
			yield return null;
		}
		SaveLoadResult? result = load.Current;
		if (result == SaveLoadResult.Corrupted && result.HasValue)
		{
			result = null;
			ShowCorruptedSaveDialog(_mode, _slot, _dlcNumber, delegate
			{
				result = SaveLoadResult.NotExist;
			}, delegate
			{
				result = SaveLoadResult.Cancel;
			});
			while (!result.HasValue)
			{
				yield return null;
			}
		}
		yield return result;
	}

	public void SaveMetaProgress(SaveSystemCallback _callback = null)
	{
		StartCoroutine(SaveMetaProgressRoutine(_callback));
	}

	protected IEnumerator SaveMetaProgressRoutine(SaveSystemCallback _callback = null)
	{
		m_savingMeta = true;
		bool abortSaving = false;
		SaveSystemStatus? status = null;
		SaveSystemCallback callback = delegate(SaveSystemStatus _status)
		{
			status = _status;
		};
		while ((!status.HasValue || status.Value.Status == SaveSystemStatus.SaveStatus.Retry) && !abortSaving)
		{
			status = null;
			TrySaveMetaProgress(callback);
			SaveSystemStatus.SaveStatus lastStatus = SaveSystemStatus.SaveStatus.COUNT;
			while ((!status.HasValue || status.Value.Status == SaveSystemStatus.SaveStatus.InProgress) && !abortSaving)
			{
				if (status.HasValue && status.Value.Status != lastStatus)
				{
					if (_callback != null)
					{
						_callback(status.Value);
					}
					if (!abortSaving)
					{
						abortSaving = CheckForClientSaveFailure(status.Value);
					}
					lastStatus = status.Value.Status;
				}
				yield return null;
			}
			if (status.HasValue && !abortSaving)
			{
				abortSaving = CheckForClientSaveFailure(status.Value);
			}
			if (!abortSaving && _callback != null && status.Value.Status != SaveSystemStatus.SaveStatus.Complete)
			{
				_callback(status.Value);
			}
		}
		m_savingMeta = false;
		if (!abortSaving)
		{
			if (_callback != null)
			{
				_callback(status.Value);
			}
		}
		else if (_callback != null)
		{
			_callback(new SaveSystemStatus(SaveSystemStatus.SaveStatus.Complete, SaveLoadResult.NotSaveable));
		}
		if (!IsSaving)
		{
			OnIdle();
		}
	}

	private void TrySaveMetaProgress(SaveSystemCallback _callback = null)
	{
		PlatformCallback callback = delegate(SaveLoadResult _result)
		{
			if (_result == SaveLoadResult.NoSpace)
			{
				CloseSpinner();
				if (_callback != null)
				{
					_callback(new SaveSystemStatus(SaveSystemStatus.SaveStatus.InProgress, SaveLoadResult.NoSpace));
				}
				ShowNoSpaceForMetaDialog(delegate
				{
					if (m_spinner == null)
					{
						m_spinner = T17DialogBoxManager.GetDialog(false);
						if (m_spinner != null)
						{
							m_spinner.Initialize("Save.Spinner.RetryingMetaSave.Title", "Save.Spinner.RetryingMetaSave.Body", null, null, null, T17DialogBox.Symbols.Spinner);
							m_spinner.Show();
						}
					}
					if (_callback != null)
					{
						_callback(new SaveSystemStatus(SaveSystemStatus.SaveStatus.Retry, SaveLoadResult.NoSpace));
					}
				}, delegate
				{
					if (_callback != null)
					{
						_callback(new SaveSystemStatus(SaveSystemStatus.SaveStatus.Complete, SaveLoadResult.Cancel));
					}
				});
			}
			else
			{
				CloseSpinner();
				if (_callback != null)
				{
					_callback(new SaveSystemStatus(SaveSystemStatus.SaveStatus.Complete, _result));
				}
			}
		};
		if (m_metaGameProgress != null)
		{
			PlatformSaveMetaProgress(callback);
		}
		else if (_callback != null)
		{
			_callback(new SaveSystemStatus(SaveSystemStatus.SaveStatus.Complete, SaveLoadResult.Cancel));
		}
	}

	protected abstract void PlatformSaveMetaProgress(PlatformCallback _callback);

	protected abstract void PlatformSaveData(SaveMode _mode, int _slot, int _dlcNumber, PlatformCallback _callback);

	private void TrySaveData(SaveMode _mode, int _slot, int _dlcNumber, SaveSystemCallback _callback = null)
	{
		PlatformCallback callback = delegate(SaveLoadResult _result)
		{
			if (_result == SaveLoadResult.NoSpace)
			{
				CloseSpinner();
				if (_callback != null)
				{
					_callback(new SaveSystemStatus(SaveSystemStatus.SaveStatus.InProgress, SaveLoadResult.NoSpace));
				}
				ShowNoSpaceForSaveDialog(delegate
				{
					if (m_spinner == null)
					{
						m_spinner = T17DialogBoxManager.GetDialog(false);
						if (m_spinner != null)
						{
							m_spinner.Initialize("Save.Spinner.RetryingSaveSlot.Title", "Save.Spinner.RetryingSaveSlot.Body", null, null, null, T17DialogBox.Symbols.Spinner);
							m_spinner.Show();
						}
					}
					if (_callback != null)
					{
						_callback(new SaveSystemStatus(SaveSystemStatus.SaveStatus.Retry, SaveLoadResult.NoSpace));
					}
				}, delegate
				{
					if (_callback != null)
					{
						_callback(new SaveSystemStatus(SaveSystemStatus.SaveStatus.Complete, SaveLoadResult.Cancel));
					}
				});
			}
			else
			{
				CloseSpinner();
				if (_result == SaveLoadResult.Exists)
				{
					SaveMetaProgress(_callback);
				}
				else if (_callback != null)
				{
					_callback(new SaveSystemStatus(SaveSystemStatus.SaveStatus.Complete, _result));
				}
			}
		};
		if (ProfileLoaded)
		{
			PlatformSaveData(_mode, _slot, _dlcNumber, callback);
		}
		else if (_callback != null)
		{
			_callback(new SaveSystemStatus(SaveSystemStatus.SaveStatus.Complete, SaveLoadResult.Cancel));
		}
	}

	public IEnumerator SaveData(SaveMode _mode, int _slot, int _dlcNumber, SaveSystemCallback _callback = null)
	{
		m_savingProgress = true;
		bool abortSaving = false;
		SaveSystemStatus? status = null;
		SaveSystemCallback callback = delegate(SaveSystemStatus _status)
		{
			status = _status;
		};
		while ((!status.HasValue || status.Value.Status == SaveSystemStatus.SaveStatus.Retry) && !abortSaving)
		{
			status = null;
			TrySaveData(_mode, _slot, _dlcNumber, callback);
			SaveSystemStatus.SaveStatus lastStatus = SaveSystemStatus.SaveStatus.COUNT;
			while (!status.HasValue || (status.Value.Status == SaveSystemStatus.SaveStatus.InProgress && !abortSaving))
			{
				if (status.HasValue && status.Value.Status != lastStatus)
				{
					if (_callback != null)
					{
						_callback(status.Value);
					}
					if (!abortSaving)
					{
						abortSaving = CheckForClientSaveFailure(status.Value);
					}
					lastStatus = status.Value.Status;
				}
				yield return null;
			}
			if (status.HasValue && !abortSaving)
			{
				abortSaving = CheckForClientSaveFailure(status.Value);
			}
			if (!abortSaving && _callback != null && status.Value.Status != SaveSystemStatus.SaveStatus.Complete)
			{
				_callback(status.Value);
			}
		}
		m_savingProgress = false;
		if (!abortSaving)
		{
			if (_callback != null)
			{
				_callback(status.Value);
			}
		}
		else if (_callback != null)
		{
			_callback(new SaveSystemStatus(SaveSystemStatus.SaveStatus.Complete, SaveLoadResult.NotSaveable));
		}
		if (!IsSaving)
		{
			OnIdle();
		}
	}

	private bool CheckForClientSaveFailure(SaveSystemStatus _status)
	{
		if (ConnectionStatus.IsInSession() && !ConnectionStatus.IsHost() && _status.Result == SaveLoadResult.NoSpace)
		{
			T17DialogBox dialog = T17DialogBoxManager.GetDialog(false);
			if (dialog != null)
			{
				dialog.Initialize("ClientSave.SaveFailed.Title", "ClientSave.SaveFailed.Message", "ClientSave.SaveFailed.Confirm", string.Empty, string.Empty);
				dialog.Show();
			}
			return true;
		}
		return false;
	}

	public abstract IEnumerator HasMetaSaveFile(ReturnValue<SaveLoadResult> _result);

	public abstract IEnumerator HasSaveFile(SaveMode _type, int _slot, int _dlcNumber, ReturnValue<SaveLoadResult> _hasSave);

	public abstract void DeleteSave(SaveMode _type, int _slot, int _dlcNumber, CallbackVoid _callback = null);

	public abstract void DeleteMetaSave(CallbackVoid _callback = null);

	private void OnIdle()
	{
		int num = 0;
		for (int i = 0; i < m_idleCallbacks.Count; i++)
		{
			m_idleCallbacks[i]();
			num++;
			if (IsSaving)
			{
				break;
			}
		}
		m_idleCallbacks.RemoveRange(0, num);
	}

	public void RegisterOnIdle(GenericVoid _callback)
	{
		if (IsSaving)
		{
			m_idleCallbacks.Add(_callback);
		}
		else
		{
			_callback();
		}
	}

	public void UnregisterOnIdle(GenericVoid _callback)
	{
		m_idleCallbacks.Remove(_callback);
	}

	private void ShowDialog(string _title, string _message, string _confirmText, string _declineText, string _cancelText, T17DialogBox.DialogEvent _confirmCallback, T17DialogBox.DialogEvent _declineCallback, T17DialogBox.DialogEvent _cancelCallback, T17DialogBox.Symbols _symbol)
	{
		m_activeInputDialog = T17DialogBoxManager.GetDialog(false);
		if (m_activeInputDialog != null)
		{
			m_activeInputDialog.Initialize(_title, _message, _confirmText, _declineText, _cancelText, _symbol);
			if (_confirmCallback != null)
			{
				T17DialogBox activeInputDialog = m_activeInputDialog;
				activeInputDialog.OnConfirm = (T17DialogBox.DialogEvent)Delegate.Combine(activeInputDialog.OnConfirm, (T17DialogBox.DialogEvent)Delegate.Combine(_confirmCallback, m_activeInputHideCallback));
			}
			if (_declineCallback != null)
			{
				T17DialogBox activeInputDialog2 = m_activeInputDialog;
				activeInputDialog2.OnDecline = (T17DialogBox.DialogEvent)Delegate.Combine(activeInputDialog2.OnDecline, (T17DialogBox.DialogEvent)Delegate.Combine(_declineCallback, m_activeInputHideCallback));
			}
			if (_cancelCallback != null)
			{
				T17DialogBox activeInputDialog3 = m_activeInputDialog;
				activeInputDialog3.OnCancel = (T17DialogBox.DialogEvent)Delegate.Combine(activeInputDialog3.OnCancel, (T17DialogBox.DialogEvent)Delegate.Combine(_cancelCallback, m_activeInputHideCallback));
			}
			m_activeInputDialog.Show();
		}
	}

	protected void ShowNoSpaceForMetaDialog(T17DialogBox.DialogEvent _retryCallback, T17DialogBox.DialogEvent _cancelCallback)
	{
		ShowDialog("MetaSave.NoSpace.Title", "MetaSave.NoSpace.Body", "Text.Button.Retry", null, null, _retryCallback, null, _cancelCallback, T17DialogBox.Symbols.Error);
	}

	protected void ShowNoSpaceForSaveDialog(T17DialogBox.DialogEvent _retryCallback, T17DialogBox.DialogEvent _cancelCallback)
	{
		ShowDialog("SaveSlot.NoSpace.Title", "SaveSlot.NoSpace.Body", "Text.Button.Retry", null, null, _retryCallback, null, _cancelCallback, T17DialogBox.Symbols.Error);
	}

	protected void ShowCorruptedMetaDialog(PlatformCallback _callback)
	{
		T17DialogBox.DialogEvent metaDeletedCallback = delegate
		{
			_callback(SaveLoadResult.NotExist);
		};
		T17DialogBox.DialogEvent cancelCallback = delegate
		{
			_callback(SaveLoadResult.Cancel);
		};
		ShowCorruptedMetaDialog(metaDeletedCallback, cancelCallback);
	}

	protected void ShowCorruptedMetaDialog(T17DialogBox.DialogEvent _metaDeletedCallback, T17DialogBox.DialogEvent _cancelCallback)
	{
		ShowDialog("MetaSave.CorruptedConfirmDelete.Title", "MetaSave.CorruptedConfirmDelete.Body", "Text.Button.Delete", null, "Text.Button.Cancel", delegate
		{
			DeleteMetaSave(delegate
			{
				_metaDeletedCallback();
			});
		}, null, _cancelCallback, T17DialogBox.Symbols.Error);
	}

	protected void ShowCorruptedSaveDialog(SaveMode _type, int _slotNum, int _dlcNumber, T17DialogBox.DialogEvent _saveDeletedCallback, T17DialogBox.DialogEvent _cancelCallback)
	{
		ShowDialog("Save.CorruptedConfirmDelete.Title", "Save.CorruptedConfirmDelete.Body", "Text.Button.Delete", null, "Text.Button.Cancel", delegate
		{
			DeleteSave(_type, _slotNum, _dlcNumber, delegate
			{
				_saveDeletedCallback();
			});
		}, null, _cancelCallback, T17DialogBox.Symbols.Error);
	}

	protected void CloseSpinner()
	{
		if (m_spinner != null)
		{
			m_spinner.Hide();
			m_spinner = null;
		}
	}

	public bool HasActiveInputDialog()
	{
		return m_activeInputDialog != null && m_activeInputDialog.IsActive;
	}

	public void CancelActiveInputDialog()
	{
		if (HasActiveInputDialog())
		{
			m_activeInputDialog.Cancel();
		}
		m_activeInputDialog = null;
	}
}
