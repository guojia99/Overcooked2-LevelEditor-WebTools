using UnityEngine;

public static class AnimatorUtils
{
	public static bool HasParameter(this Animator _animator, int _paramHash)
	{
		AnimatorControllerParameter[] parameters = _animator.parameters;
		for (int i = 0; i < parameters.Length; i++)
		{
			if (parameters[i].nameHash == _paramHash)
			{
				return true;
			}
		}
		return false;
	}

	public static bool HasParameter(this Animator _animator, string _paramName)
	{
		AnimatorControllerParameter[] parameters = _animator.parameters;
		for (int i = 0; i < parameters.Length; i++)
		{
			if (parameters[i].name == _paramName)
			{
				return true;
			}
		}
		return false;
	}

	public static bool IsActive(this Animator _animator)
	{
		if (_animator == null)
		{
			return false;
		}
		if (!_animator.gameObject.activeInHierarchy)
		{
			return false;
		}
		if (_animator.runtimeAnimatorController == null)
		{
			return false;
		}
		return true;
	}

	public static object GetValue(Animator _animator, string _animatorVariableName, AnimatorVariableType _valueType)
	{
		switch (_valueType)
		{
		case AnimatorVariableType.Float:
			return _animator.GetFloat(_animatorVariableName);
		case AnimatorVariableType.Int:
			return _animator.GetInteger(_animatorVariableName);
		case AnimatorVariableType.Bool:
			return _animator.GetBool(_animatorVariableName);
		case AnimatorVariableType.Trigger:
			return false;
		default:
			return null;
		}
	}

	public static object GetValue(Animator _animator, int _animatorVariableNameHash, AnimatorVariableType _valueType)
	{
		switch (_valueType)
		{
		case AnimatorVariableType.Float:
			return _animator.GetFloat(_animatorVariableNameHash);
		case AnimatorVariableType.Int:
			return _animator.GetInteger(_animatorVariableNameHash);
		case AnimatorVariableType.Bool:
			return _animator.GetBool(_animatorVariableNameHash);
		case AnimatorVariableType.Trigger:
			return false;
		default:
			return null;
		}
	}

	public static object SetValue(Animator _animator, string _animatorVariableName, AnimatorVariableType _valueType, object _value)
	{
		switch (_valueType)
		{
		case AnimatorVariableType.Float:
			_animator.SetFloat(_animatorVariableName, (float)_value);
			break;
		case AnimatorVariableType.Int:
			_animator.SetInteger(_animatorVariableName, (int)_value);
			break;
		case AnimatorVariableType.Bool:
			_animator.SetBool(_animatorVariableName, (bool)_value);
			break;
		case AnimatorVariableType.Trigger:
			if ((bool)_value)
			{
				_animator.SetTrigger(_animatorVariableName);
			}
			else
			{
				_animator.ResetTrigger(_animatorVariableName);
			}
			break;
		}
		return null;
	}

	public static object SetValue(Animator _animator, int _animatorVariableNameHash, AnimatorVariableType _valueType, object _value)
	{
		switch (_valueType)
		{
		case AnimatorVariableType.Float:
			_animator.SetFloat(_animatorVariableNameHash, (float)_value);
			break;
		case AnimatorVariableType.Int:
			_animator.SetInteger(_animatorVariableNameHash, (int)_value);
			break;
		case AnimatorVariableType.Bool:
			_animator.SetBool(_animatorVariableNameHash, (bool)_value);
			break;
		case AnimatorVariableType.Trigger:
			if ((bool)_value)
			{
				_animator.SetTrigger(_animatorVariableNameHash);
			}
			else
			{
				_animator.ResetTrigger(_animatorVariableNameHash);
			}
			break;
		}
		return null;
	}
}
