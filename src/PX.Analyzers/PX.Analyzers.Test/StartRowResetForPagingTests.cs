﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using PX.Analyzers.Analyzers;
using PX.Analyzers.Test.Helpers;
using TestHelper;
using Xunit;
using Microsoft.CodeAnalysis.CodeFixes;

namespace PX.Analyzers.Test
{
    public class StartRowResetForPagingTests : CodeFixVerifier
    {
	    private DiagnosticResult[] CreateDiagnosticResults()
	    {
		    return new DiagnosticResult[] 
            {
                new DiagnosticResult
                {
                    Id = Descriptors.PX1010_StartRowResetForPaging.Id,
                    Message = Descriptors.PX1010_StartRowResetForPaging.Title.ToString(),
                    Severity = DiagnosticSeverity.Warning,
                    Locations =
                        new[] { new DiagnosticResultLocation("Test0.cs", 17, 44) }
                }
            };
	    }

        [Theory]
        [EmbeddedFileData("StartRowResetForPaging.cs")]
        public void TestDiagnostic(string actual)
        {
            VerifyCSharpDiagnostic(actual, CreateDiagnosticResults());
        }

        [Theory]
        [EmbeddedFileData("StartRowResetForPaging.cs", "StartRowResetForPaging_Expected.cs")]
        public void TestCodeFix(string actual, string expected)
        {
            VerifyCSharpFix(actual, expected);
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new StartRowResetForPagingAnalyzer();
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new StartRowResetForPagingFix();
        }
    }
}
