﻿using System;


namespace Acuminator.Vsix.Utilities
{
    /// <summary>
    /// Constants for VSIX project
    /// </summary>
    internal static class Constants
	{
		public static class CSharp
		{
			/// <summary>
			/// The Visual Studio legacy C# language name used by the <see cref="EnvDTE.Document.Language"/> library.
			/// </summary>
			public const string LegacyLanguageName = "CSharp";

			/// <summary>
			/// C# file extension.
			/// </summary>
			public const string FileExtension = ".cs";
		}

		public static class Settings
		{
			public const string All = "All";
			public const string BannedApiFilePath = nameof(GeneralOptionsPage.BannedApiFilePath);
			public const string WhiteListApiFilePath = nameof(GeneralOptionsPage.WhiteListApiFilePath);

			public static class Coloring
			{
				public const bool ColoringEnabledDefault 		 = true;
				public const bool PXActionColoringEnabledDefault = true;
				public const bool PXGraphColoringEnabledDefault  = true;
				public const bool ColorOnlyInsideBQLDefault 	 = false;
				public const bool UseRegexColoringDefault 		 = false;
			}

			public static class Outlining
			{
				public const bool UseBqlOutliningDefault 		 = true;
				public const bool UseBqlDetailedOutliningDefault = true;
			}
		}

		/// <summary>
		/// Banned API related constants.
		/// </summary>
		public static class BannedApi
		{
			public const string BannnedApiFolder = "Acumatica Banned API";
		}

		/// <summary>
		/// Code snippets related constants.
		/// </summary>
		public static class CodeSnippets
		{
			public const string FileExtension = ".snippet";

			public const string CodeSnippetsFolder = "Acumatica Code Snippets";

			public const string DacSnippetsFolder = "Acumatica DAC Snippets";
			public const string DacFieldSnippetsSubFolder = "DAC Field";

			public const string NCGraphEventsFolder = "Acumatica Name Convention Events";
			public const string NCFieldEventsSubFolder = "Field Events";
			public const string NCRowEventsSubFolder = "Row Events";
			public const string NCUnspecifiedEventsSubFolder = "Unspecified Event";

			public const string GenericGraphEventsFolder = "Acumatica Generic Events";

			public const string GenericFieldEventsWithFullSignatureSubFolder = "Field Events Full Signature";
			public const string GenericUnspecifiedFieldEventsWithFullSignatureSubFolder = "Unspecified Event";

			public const string GenericFieldEventsWithShortSignatureSubFolder = "Field Events Short Signature";
			public const string GenericUnspecifiedFieldEventsWithShortSignatureSubFolder = "Unspecified Event";

			public const string GenericRowEventsSubFolder = "Row Events";
			public const string GenericUnspecifiedRowEventsSubFolder = "Unspecified Event";
		}
	}
}
