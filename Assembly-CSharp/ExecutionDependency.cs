using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ExecutionDependency : Attribute
{
	public Type Dependency;

	public ExecutionDependency(Type _dependency)
	{
		Dependency = _dependency;
	}
}
