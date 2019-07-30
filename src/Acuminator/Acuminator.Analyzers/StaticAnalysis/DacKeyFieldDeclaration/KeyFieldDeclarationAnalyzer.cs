﻿using Acuminator.Utilities.DiagnosticSuppression;
using Acuminator.Utilities.Roslyn.PXFieldAttributes;
using Acuminator.Utilities.Roslyn.Semantic;
using Acuminator.Utilities.Roslyn.Semantic.Dac;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Acuminator.Utilities.Roslyn.Constants;
using Acuminator.Analyzers.StaticAnalysis.Dac;

namespace Acuminator.Analyzers.StaticAnalysis.DacKeyFieldDeclaration
{
	public class KeyFieldDeclarationAnalyzer : DacAggregatedAnalyzerBase
	{
		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
			ImmutableArray.Create
			(
				Descriptors.PX1055_DacKeyFieldsWithIdentityKeyField
			);

		public override void Analyze(SymbolAnalysisContext context, PXContext pxContext, DacSemanticModel dac)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			var attributeInformation = new AttributeInformation(pxContext);
			var keyAttributes = new List<AttributeData>(capacity: 2);

			bool containsNaturalPrimaryKeys = false;
			bool containsIdentityKeys = false;
			var propertyAttributes = dac.DeclaredProperties.SelectMany(property => property.Symbol.GetAttributes());

			foreach (var attribute in propertyAttributes)
			{
				context.CancellationToken.ThrowIfCancellationRequested();
				bool isAttributeWithPrimaryKey = attribute.NamedArguments.Any(arg => arg.Key.Contains(DelegateNames.IsKey) && 
																			  arg.Value.Value is bool isKeyValue && isKeyValue == true);
				if (isAttributeWithPrimaryKey)
				{
					if (!containsNaturalPrimaryKeys || !containsIdentityKeys)  //If we already know that DAC contains both then no need to analyse more
					{
						bool isIdentityAttribute = IsDerivedFromIdentityTypes(attribute, pxContext, attributeInformation);
						containsNaturalPrimaryKeys = containsNaturalPrimaryKeys || !isIdentityAttribute;
						containsIdentityKeys = containsIdentityKeys || isIdentityAttribute;
					}

					keyAttributes.Add(attribute);
				}
			}

			if (containsNaturalPrimaryKeys && containsIdentityKeys)
			{
				var locations = keyAttributes.Select(attribute => GetAttributeLocation(attribute, context.CancellationToken)).ToList();

				foreach (Location attributeLocation in locations)
				{
					var extraLocations = locations.Where(l => l != attributeLocation);

					context.ReportDiagnosticWithSuppressionCheck(
						Diagnostic.Create(
							Descriptors.PX1055_DacKeyFieldsWithIdentityKeyField, attributeLocation, extraLocations),
							pxContext.CodeAnalysisSettings);
				}
			}
		}

		private static bool IsDerivedFromIdentityTypes(AttributeData attribute, PXContext pxContext, AttributeInformation attributeInformation) =>
			attributeInformation.IsAttributeDerivedFromClass(attribute.AttributeClass, pxContext.FieldAttributes.PXDBIdentityAttribute) ||
			attributeInformation.IsAttributeDerivedFromClass(attribute.AttributeClass, pxContext.FieldAttributes.PXDBLongIdentityAttribute);

		private static Location GetAttributeLocation(AttributeData attribute, CancellationToken cancellationToken) =>
			attribute.ApplicationSyntaxReference
					?.GetSyntax(cancellationToken)
					?.GetLocation();	
	}
}
