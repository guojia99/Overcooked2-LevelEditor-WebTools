#define ANALYTICS
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEngine;

public class ExceptionManager : Manager
{
	private class ExceptionInfo
	{
		public string m_exceptionString = string.Empty;

		public string m_stackTrace = string.Empty;

		public void Set(string exceptionString, string stackTrace)
		{
			m_exceptionString = exceptionString;
			m_stackTrace = stackTrace;
		}

		public bool IsValid()
		{
			return !string.IsNullOrEmpty(m_exceptionString) && !string.IsNullOrEmpty(m_stackTrace);
		}
	}

	private ExceptionInfo m_LastException = new ExceptionInfo();

	private void Awake()
	{
	}

	private void Start()
	{
	}

	private void OnEnable()
	{
		Application.logMessageReceived += HandleException;
	}

	private void OnDisable()
	{
		Application.logMessageReceived -= HandleException;
	}

	private void OnDestroy()
	{
	}

	private void HandleException(string logString, string stackTrace, LogType type)
	{
		if (type == LogType.Exception)
		{
			m_LastException.Set(logString, stackTrace);
			LogLastException();
			DisplayLastException(true);
		}
	}

	private void LogLastException()
	{
	}

	private void DisplayLastException(bool bJustOccured)
	{
	}

	public void LogACaughtException(Exception e, string log)
	{
		string userData = GameUtils.DebugGetState();
		string stackTrace = e.ToString();
		Analytics analytics = GameUtils.RequestManager<Analytics>();
		if (null != analytics)
		{
			analytics.LogAnException(log, stackTrace, userData);
		}
		HandleException(log, stackTrace, LogType.Exception);
	}

	public void LogAnException(string log)
	{
		StackTrace stackTrace = new StackTrace(1, true);
		string text = string.Empty;
		if (stackTrace != null)
		{
			for (int i = 0; i < stackTrace.FrameCount; i++)
			{
				StackFrame frame = stackTrace.GetFrame(i);
				if (frame == null)
				{
					continue;
				}
				MethodBase method = frame.GetMethod();
				string text2;
				if (method != null)
				{
					ParameterInfo[] parameters = method.GetParameters();
					if (parameters != null && method.ReflectedType != null)
					{
						text2 = text;
						text = text2 + method.ReflectedType.Name + "." + method.Name + "( ";
						for (int j = 0; j < parameters.Length; j++)
						{
							if (parameters[j] != null)
							{
								if (j != 0)
								{
									text += ", ";
								}
								text2 = text;
								text = string.Concat(text2, parameters[j].ParameterType, " ", parameters[j].Name);
							}
						}
					}
					text += " )";
				}
				string fileName = frame.GetFileName();
				string text3 = string.Empty;
				if (frame.GetFileName() != null)
				{
					int num = frame.GetFileName().LastIndexOf('\\') + 1;
					text3 = ((num == -1) ? Path.GetFileName(fileName) : fileName.Substring(num));
				}
				text2 = text;
				text = text2 + " in " + text3 + ":" + frame.GetFileLineNumber() + " \n";
			}
		}
		string userData = GameUtils.DebugGetState();
		Analytics analytics = GameUtils.RequestManager<Analytics>();
		if (null != analytics)
		{
			analytics.LogAnException(log, text, userData);
		}
		HandleException(log, text, LogType.Exception);
	}
}
