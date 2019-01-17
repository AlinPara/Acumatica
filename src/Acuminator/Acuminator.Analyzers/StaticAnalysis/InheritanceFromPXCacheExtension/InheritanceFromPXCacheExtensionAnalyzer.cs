﻿using Acuminator.Utilities.DiagnosticSuppression;
using Acuminator.Utilities.Roslyn.Semantic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using PX.Data;
using System.Collections.Immutable;
using System.Linq;

namespace Acuminator.Analyzers.StaticAnalysis.InheritanceFromPXCacheExtension
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class InheritanceFromPXCacheExtensionAnalyzer : PXDiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
				Descriptors.PX1009_InheritanceFromPXCacheExtension,
				Descriptors.PX1011_InheritanceFromPXCacheExtension);
        internal override void AnalyzeCompilation(CompilationStartAnalysisContext compilationStartContext, PXContext pxContext)
        {
            compilationStartContext.RegisterSymbolAction(c => Analyze(c, pxContext), 
                SymbolKind.NamedType);
        }

        private void Analyze(SymbolAnalysisContext context, PXContext pxContext)
        {
	        var symbol = (INamedTypeSymbol) context.Symbol;
	        if (!symbol.InheritsFrom(pxContext.PXCacheExtensionType)
	            || symbol.Name == nameof(PXCacheExtension)
				|| symbol.InheritsFromOrEquals(pxContext.PXMappedCacheExtensionType))
	        {
		        return;
	        }

	        if (symbol.BaseType.Name == nameof(PXCacheExtension))
	        {
		        if (!symbol.IsSealed)
		        {
			        context.ReportDiagnosticWithSuppressionCheck(
						Diagnostic.Create(Descriptors.PX1011_InheritanceFromPXCacheExtension, symbol.Locations.First()));
		        }
	        }
	        else
	        {
		        context.ReportDiagnosticWithSuppressionCheck(
					Diagnostic.Create(Descriptors.PX1009_InheritanceFromPXCacheExtension, symbol.Locations.First()));
	        }
        }
    }
}
