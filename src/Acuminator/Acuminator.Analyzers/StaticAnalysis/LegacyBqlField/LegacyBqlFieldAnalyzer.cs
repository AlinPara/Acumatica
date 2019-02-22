﻿using System;
using System.Collections.Immutable;
using System.Linq;
using Acuminator.Utilities.Roslyn.Semantic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Acuminator.Utilities.DiagnosticSuppression;

namespace Acuminator.Analyzers.StaticAnalysis.LegacyBqlField
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class LegacyBqlFieldAnalyzer : PXDiagnosticAnalyzer
	{
		public const string CorrespondingPropertyType = "CorrespondingPropertyType";
		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Descriptors.PX1060_LegacyBqlField);

		protected override bool ShouldAnalyze(PXContext pxContext) => pxContext.IsAcumatica2019R1;

		internal override void AnalyzeCompilation(CompilationStartAnalysisContext compilationStartContext, PXContext pxContext)
		{
			compilationStartContext.RegisterSymbolAction(
				c => Analyze(c, pxContext),
				SymbolKind.NamedType);
		}

		private async void Analyze(SymbolAnalysisContext context, PXContext pxContext)
		{
			if (context.Symbol is INamedTypeSymbol dacFieldType)
			{
				if (!IsDacFieldType(dacFieldType, pxContext) || AlreadyStronglyTyped(dacFieldType, pxContext))
					return;

				context.CancellationToken.ThrowIfCancellationRequested();

				Location location = dacFieldType.Locations.FirstOrDefault();
				if (location != null)
				{
					var property = dacFieldType.ContainingType
						.GetBaseTypesAndThis()
						.SelectMany(t => t.GetMembers().OfType<IPropertySymbol>())
						.FirstOrDefault(f => String.Equals(f.Name, dacFieldType.Name, StringComparison.OrdinalIgnoreCase));

					if (property != null)
					{
						string propertyTypeName = property.Type is IArrayTypeSymbol arrType
							? arrType.ElementType.Name + "[]"
							: property.Type is INamedTypeSymbol namedType
								? namedType.IsNullable(pxContext)
									? namedType.GetUnderlyingTypeFromNullable(pxContext).Name
									: namedType.Name
								: null;

						if (propertyTypeName == null || !LegacyBqlFieldFix.PropertyTypeToFieldType.ContainsKey(propertyTypeName))
							return;

						context.CancellationToken.ThrowIfCancellationRequested();

						var properties = ImmutableDictionary.CreateBuilder<string, string>();
						properties.Add(CorrespondingPropertyType, propertyTypeName);
						context.ReportDiagnosticWithSuppressionCheck(Diagnostic.Create(Descriptors.PX1060_LegacyBqlField, location, properties.ToImmutable(), dacFieldType.Name));
					}
				}
			}
		}

		private static bool IsDacFieldType(ITypeSymbol dacFieldType, PXContext pxContext)
			=> dacFieldType.TypeKind == TypeKind.Class &&
				dacFieldType.IsDacField() &&
				dacFieldType.BaseType.SpecialType == SpecialType.System_Object &&
				dacFieldType.ContainingType != null &&
				dacFieldType.ContainingType.IsDacOrExtension(pxContext);

		internal static bool AlreadyStronglyTyped(INamedTypeSymbol dacFieldType, PXContext pxContext)
			=> dacFieldType.AllInterfaces.Any(t =>
				t.IsGenericType
				&& t.OriginalDefinition.Name == pxContext.IImplementType.Name
				&& t.TypeArguments.First().AllInterfaces.Any(z => z.Name == pxContext.BqlTypes.BqlDataType.Name));
	}
}