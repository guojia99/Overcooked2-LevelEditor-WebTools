using System;
using System.Collections;
using System.Collections.Generic;

public static class DebugAdvanceSystem
{
	private class Job
	{
		public Func<bool> Completed { get; private set; }

		public Action ContinueWith { get; private set; }

		public Job(Func<bool> completed, Action continueWith)
		{
			Completed = completed;
			ContinueWith = continueWith;
		}
	}

	private static readonly List<Job> jobs = new List<Job>();

	private static readonly List<IEnumerator> coroutines = new List<IEnumerator>();

	public static void Add(Func<bool> completed, Action continueWith)
	{
	}

	public static void Add(IEnumerator _coroutine)
	{
	}
}
