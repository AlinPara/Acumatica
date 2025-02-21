﻿#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Shell;

namespace Acuminator.Vsix.ToolWindows.CodeMap
{
	/// <summary>
	/// Open CodeMap Window command.
	/// </summary>
	internal sealed class OpenCodeMapWindowCommand : OpenToolWindowCommandBase<CodeMapWindow>
	{
		private static int _isCommandInitialized = NOT_INITIALIZED;

		/// <summary>
		/// Command ID.
		/// </summary>
		public const int CommandId = 0x0104;

		private OpenCodeMapWindowCommand(AsyncPackage package, OleMenuCommandService commandService) : 
									base(package, commandService, CommandId)
		{		
		}

		/// <summary>
		/// Gets the instance of the command.
		/// </summary>
		public static OpenCodeMapWindowCommand? Instance
		{
			get;
			private set;
		}

#pragma warning disable CS8774 // Member must have a non-null value when exiting.
		/// <summary>
		/// Initializes the singleton instance of the command.
		/// </summary>
		/// <param name="package">Owner package, not null.</param>
		/// <param name="oleCommandService">The OLE command service, not null.</param>
		[MemberNotNull(nameof(Instance))]
		public static void Initialize(AsyncPackage package, OleMenuCommandService oleCommandService)
		{
			if (Interlocked.CompareExchange(ref _isCommandInitialized, value: INITIALIZED, comparand: NOT_INITIALIZED) == NOT_INITIALIZED)
			{
				Instance = new OpenCodeMapWindowCommand(package, oleCommandService);
			}
		}
#pragma warning restore CS8774

		protected override async Task<CodeMapWindow?> OpenToolWindowAsync()
		{
			CodeMapWindow? codeMapWindow = await base.OpenToolWindowAsync();
			return codeMapWindow;
		}
	}
}
