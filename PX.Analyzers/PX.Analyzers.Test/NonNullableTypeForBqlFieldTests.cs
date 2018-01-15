﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using PX.Analyzers.Analyzers;
using PX.Analyzers.FixProviders;
using PX.Analyzers.Test.Helpers;
using TestHelper;
using Xunit;

namespace PX.Analyzers.Test
{
    public class NonNullableTypeForBqlFieldTests : CodeFixVerifier
    {
	    private DiagnosticResult CreatePX1014DiagnosticResult(int line, int column)
	    {
			var diagnostic = new DiagnosticResult
			{
				Id = Descriptors.PX1014_NonNullableTypeForBqlField.Id,
				Message = Descriptors.PX1014_NonNullableTypeForBqlField.Title.ToString(),
				Severity = DiagnosticSeverity.Error,
				Locations =
					new[] {
						new DiagnosticResultLocation("Test0.cs", line, column)
					}
			};

		    return diagnostic;
	    }

	    [Theory]
	    [EmbeddedFileData("NonNullableTypeForBqlField.cs")]
	    public void TestDiagnostic(string actual)
	    {
		    VerifyCSharpDiagnostic(actual, CreatePX1014DiagnosticResult(16, 10));
	    }

		[Theory]
        [EmbeddedFileData("NonNullableTypeForBqlField_Expected.cs")]
        public void TestDiagnostic_ShouldNotShowDiagnostic(string actual)
        {
            VerifyCSharpDiagnostic(actual);
        }

	    [Theory]
	    [EmbeddedFileData("NonNullableTypeForBqlField.cs", "NonNullableTypeForBqlField_Expected.cs")]
	    public void TestCodeFix(string actual, string expected)
	    {
		    VerifyCSharpFix(actual, expected);
	    }

		protected override CodeFixProvider GetCSharpCodeFixProvider()
		{
			return new NonNullableTypeForBqlFieldFix();
		}

		protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new NonNullableTypeForBqlFieldAnalyzer();
        }
    }
}
