﻿using Acuminator.Analyzers;
using Acuminator.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using TestHelper;
using Xunit;

namespace Acuminator.Tests
{
    public class PXGraphUsageInDacTests : DiagnosticVerifier
    {
        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new DacGraphUsageAnalyzer();

        private DiagnosticResult CreatePX1029DiagnosticResult(int line, int column)
        {
            return new DiagnosticResult
            {
                Id = Descriptors.PX1029_PXGraphUsageInDacProperty.Id,
                Message = Descriptors.PX1029_PXGraphUsageInDacProperty.Title.ToString(),
                Severity = DiagnosticSeverity.Error,
                Locations = new[] { new DiagnosticResultLocation("Test0.cs", line, column) }
            };
        }

        [Theory]
        [EmbeddedFileData(@"Dac\PX1029\Diagnostics\DacWithGraphUsage.cs")]
        public void Test_PXGraph_Usage_Inside_Dac(string source)
        {
            VerifyCSharpDiagnostic(source/*,
                CreatePX1029DiagnosticResult(15, 17),
                CreatePX1029DiagnosticResult(15, 50),
                CreatePX1029DiagnosticResult(17, 24),
                CreatePX1029DiagnosticResult(27, 24),
                CreatePX1029DiagnosticResult(27, 42),
                CreatePX1029DiagnosticResult(37, 24)*/);
        }

        [Theory]
        [EmbeddedFileData(@"Dac\PX1029\Diagnostics\DacExtensionWithGraphUsage.cs")]
        public void Test_PXGraph_Usage_Inside_CacheExtension(string source)
        {
            VerifyCSharpDiagnostic(source,
                CreatePX1029DiagnosticResult(15, 17),
                CreatePX1029DiagnosticResult(15, 50),
                CreatePX1029DiagnosticResult(17, 24),
                CreatePX1029DiagnosticResult(27, 24),
                CreatePX1029DiagnosticResult(27, 42),
                CreatePX1029DiagnosticResult(37, 24));
        }
    }
}
