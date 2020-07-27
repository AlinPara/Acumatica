﻿using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Acuminator.Utilities.Common;
using Acuminator.Utilities.DiagnosticSuppression;
using Acuminator.Utilities.Roslyn.Semantic;
using Acuminator.Utilities.Roslyn.Semantic.Dac;
using Acuminator.Utilities.Roslyn.Syntax;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Acuminator.Utilities.Roslyn.Constants;

namespace Acuminator.Analyzers.StaticAnalysis.DacReferentialIntegrity
{
	public class DacPrimaryKeyDeclarationAnalyzer : DacKeyDeclarationAnalyzerBase
	{
		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
			ImmutableArray.Create
			(
				Descriptors.PX1033_MissingDacPrimaryKeyDeclaration,
				Descriptors.PX1035_MultiplePrimaryKeyDeclarationsInDac,
				Descriptors.PX1036_WrongDacPrimaryKeyName
			);

		protected override bool IsKeySymbolDefined(PXContext context) => context.ReferentialIntegritySymbols.IPrimaryKey != null;

		protected override bool ShouldAnalyzeDac(PXContext context, DacSemanticModel dac) =>
			 base.ShouldAnalyzeDac(context, dac) && dac.DacProperties.Count(property => property.IsKey) > 0;

		public override void Analyze(SymbolAnalysisContext symbolContext, PXContext context, DacSemanticModel dac)
		{
			symbolContext.CancellationToken.ThrowIfCancellationRequested();

			var keyDeclarations = GetPrimaryKeyDeclarations(context, dac, symbolContext.CancellationToken).ToList(capacity: 1);

			switch (keyDeclarations.Count)
			{
				case 0:
					ReportNoPrimaryKeyDeclarationsInDac(symbolContext, context, dac);
					return;
				case 1:
					AnalyzeSinglePrimaryKeyDeclaration(symbolContext, context, keyDeclarations[0]);
					return;
				default:
					AnalyzeMultiplePrimaryKeyDeclarations(symbolContext, context, dac, keyDeclarations);
					return;
			}
		}

		private IEnumerable<INamedTypeSymbol> GetPrimaryKeyDeclarations(PXContext context, DacSemanticModel dac, CancellationToken cancellationToken) =>
			 dac.Symbol.GetFlattenedNestedTypes(shouldWalkThroughNestedTypesPredicate: nestedType => !nestedType.IsDacOrExtension(context),
												cancellationToken)
					   .Where(nestedType => nestedType.ImplementsInterface(context.ReferentialIntegritySymbols.IPrimaryKey));

		private void ReportNoPrimaryKeyDeclarationsInDac(SymbolAnalysisContext symbolContext, PXContext context, DacSemanticModel dac)
		{
			Location location = dac.Node.Identifier.GetLocation() ?? dac.Node.GetLocation();

			if (location != null)
			{
				symbolContext.ReportDiagnosticWithSuppressionCheck(
					Diagnostic.Create(Descriptors.PX1033_MissingDacPrimaryKeyDeclaration, location),
					context.CodeAnalysisSettings);
			} 
		}

		private void AnalyzeSinglePrimaryKeyDeclaration(SymbolAnalysisContext symbolContext, PXContext context, INamedTypeSymbol keyDeclaration)
		{
			if (keyDeclaration.Name == TypeNames.PrimaryKeyClassName)
				return;

			var keyDeclarationNode = keyDeclaration.GetSyntax(symbolContext.CancellationToken);
			Location location = (keyDeclarationNode as ClassDeclarationSyntax)?.Identifier.GetLocation() ?? keyDeclarationNode?.GetLocation();

			if (location == null)
				return;

			var diagnosticProperties = new Dictionary<string, string>
			{
				{ nameof(RefIntegrityDacKeyType),  RefIntegrityDacKeyType.PrimaryKey.ToString() }
			}
			.ToImmutableDictionary();

			symbolContext.ReportDiagnosticWithSuppressionCheck(
										Diagnostic.Create(Descriptors.PX1036_WrongDacPrimaryKeyName, location, diagnosticProperties),
										context.CodeAnalysisSettings);
		}

		private void AnalyzeMultiplePrimaryKeyDeclarations(SymbolAnalysisContext symbolContext, PXContext context, DacSemanticModel dac, 
														   List<INamedTypeSymbol> keyDeclarations)
		{
			symbolContext.CancellationToken.ThrowIfCancellationRequested();

			if (!CheckThatAllPrimaryKeysHaveUniqueSetsOfFields(symbolContext, context, dac, keyDeclarations))
				return;




			
		}

		private bool CheckThatAllPrimaryKeysHaveUniqueSetsOfFields(SymbolAnalysisContext symbolContext, PXContext context, DacSemanticModel dac,
																   List<INamedTypeSymbol> keyDeclarations)
		{
			var duplicateKeys = GetDuplicatePrimaryKeys(context, dac, keyDeclarations, symbolContext.CancellationToken);

			if (duplicateKeys.Count == 0)
				return true;

			var locations = duplicateKeys.Select(declaration => declaration.GetSyntax(symbolContext.CancellationToken))
										 .OfType<ClassDeclarationSyntax>()
										 .Select(keyClassDeclaration => keyClassDeclaration.Identifier.GetLocation() ??
																		keyClassDeclaration.GetLocation())
										 .Where(location => location != null)
										 .ToList(capacity: duplicateKeys.Count);
		
			for (int i = 0; i < locations.Count; i++)
			{
				Location location = locations[i];
				var otherLocations = locations.Where((_, index) => index != i);

				symbolContext.ReportDiagnosticWithSuppressionCheck(
					Diagnostic.Create(Descriptors.PX1035_MultiplePrimaryKeyDeclarationsInDac, location, otherLocations),
					context.CodeAnalysisSettings);
			}

			return false;
		}

		private HashSet<INamedTypeSymbol> GetDuplicatePrimaryKeys(PXContext context, DacSemanticModel dac, List<INamedTypeSymbol> keyDeclarations,
																  CancellationToken cancellationToken)
		{
			var processedPrimaryKeysByHash = new Dictionary<string, INamedTypeSymbol>(capacity: keyDeclarations.Count);
			var duplicateKeys = new HashSet<INamedTypeSymbol>();

			foreach (var primaryKey in keyDeclarations)
			{
				// We don't check custom IPrimaryKey implementations since it will be impossible to deduce referenced set of DAC fields in a general case.
				// Instead we only analyze primary keys made with generic class By<,...,> or derived from it. This should handle 99% of PK use cases
				var byType = primaryKey.GetBaseTypesAndThis()
									   .OfType<INamedTypeSymbol>()
									   .FirstOrDefault(type => type.Name == TypeNames.By_TypeName && !type.TypeArguments.IsDefaultOrEmpty &&
															   type.TypeArguments.All(dacFieldArg => dac.FieldsByNames.ContainsKey(dacFieldArg.Name)));
				if (byType == null)
					continue;

				cancellationToken.ThrowIfCancellationRequested();

				var stringHash = byType.TypeArguments
									   .Select(dacFieldUsedByKey => dacFieldUsedByKey.MetadataName)
									   .OrderBy(metadataName => metadataName)
									   .Join(separator: ",");

				if (processedPrimaryKeysByHash.TryGetValue(stringHash, out var processedPrimaryKey))
				{
					duplicateKeys.Add(processedPrimaryKey);
					duplicateKeys.Add(primaryKey);
				}
				else
				{
					processedPrimaryKeysByHash.Add(stringHash, primaryKey);
				}
			}

			return duplicateKeys;
		}

		private void AnalyzeDeclarationOfTwoPrimaryKeys(SymbolAnalysisContext symbolContext, PXContext context, INamedTypeSymbol keyDeclaration)
		{
			if (keyDeclaration.Name == TypeNames.PrimaryKeyClassName)
				return;

			var keyDeclarationNode = keyDeclaration.GetSyntax(symbolContext.CancellationToken);
			Location location = (keyDeclarationNode as ClassDeclarationSyntax)?.Identifier.GetLocation() ?? keyDeclarationNode?.GetLocation();

			if (location == null)
				return;

			var diagnosticProperties = new Dictionary<string, string>
			{
				{ nameof(RefIntegrityDacKeyType),  RefIntegrityDacKeyType.PrimaryKey.ToString() }
			}
			.ToImmutableDictionary();

			symbolContext.ReportDiagnosticWithSuppressionCheck(
										Diagnostic.Create(Descriptors.PX1036_WrongDacPrimaryKeyName, location, diagnosticProperties),
										context.CodeAnalysisSettings);
		}

	}
}