﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace PX.Analyzers.Vsix.Formatter
{
	public class BqlFormatter
	{
		private readonly SyntaxTriviaList EndOfLineTrivia;
		private readonly SyntaxTriviaList IndentationTrivia;

		public BqlFormatter(string endOfLineCharacter, bool useTabs, int tabSize, int indentSize)
		{
			EndOfLineTrivia = SyntaxTriviaList.Create(SyntaxFactory.EndOfLine(endOfLineCharacter));
			if (useTabs || indentSize >= tabSize)
			{
				var items = Enumerable
					.Repeat(SyntaxFactory.Tab, indentSize / tabSize)
					.Append(SyntaxFactory.Whitespace(new string(' ', indentSize % tabSize)));
				IndentationTrivia = SyntaxTriviaList.Empty.AddRange(items);
			}
			else
			{
				IndentationTrivia = SyntaxTriviaList.Create(SyntaxFactory.Whitespace(new string(' ', indentSize)));
			}
		}

		public SyntaxNode Format(SyntaxNode syntaxRoot, SemanticModel semanticModel)
		{
			var planner = new BqlRewritingPlanner(new BqlContext(semanticModel.Compilation), semanticModel,
				EndOfLineTrivia, IndentationTrivia);
			planner.Visit(syntaxRoot);
			
			var rewriter = new LeadingTriviaRewriter(planner.Result);
			return rewriter.Visit(syntaxRoot);
		}
	}
}
