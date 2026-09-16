using System;

namespace InControl
{
	public class BindingListenOptions
	{
		public bool IncludeControllers = true;

		public bool IncludeUnknownControllers;

		public bool IncludeNonStandardControls = true;

		public bool IncludeMouseButtons;

		public bool IncludeKeys = true;

		public bool IncludeModifiersAsFirstClassKeys;

		public uint MaxAllowedBindings;

		public uint MaxAllowedBindingsPerType;

		public bool AllowDuplicateBindingsPerSet;

		public bool UnsetDuplicateBindingsOnSet;

		public BindingSource ReplaceBinding;

		public Func<PlayerAction, BindingSource, bool> OnBindingFound;

		public Action<PlayerAction, BindingSource> OnBindingAdded;

		public Action<PlayerAction, BindingSource, BindingSourceRejectionType> OnBindingRejected;
	}
}
