﻿using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PX.Analyzers.Utilities;

namespace PX.Analyzers.Vsix.Formatter
{
	/// <summary>
	/// Collects all modifications without modifying original syntax tree
	/// </summary>
	class BqlRewritingPlanner : BqlRewritingPlannerBase
	{
		private readonly Stack<SyntaxTriviaList> _currentIndentation = new Stack<SyntaxTriviaList>();

		public BqlRewritingPlanner(BqlContext context, SemanticModel semanticModel,
			SyntaxTriviaList endOfLineTrivia, SyntaxTriviaList indentationTrivia)
			: base(context, semanticModel, endOfLineTrivia, indentationTrivia, SyntaxTriviaList.Empty)
		{
		}


		public override void VisitGenericName(GenericNameSyntax node)
		{
			INamedTypeSymbol typeSymbol = GetTypeSymbol(node);
			INamedTypeSymbol constructedFromSymbol = typeSymbol?.ConstructedFrom; // get generic type

			bool withIndent = false;

			if (constructedFromSymbol != null)
			{
				if (constructedFromSymbol.InheritsFromOrEquals(Context.PXSelectBase)
				         || constructedFromSymbol.ImplementsInterface(Context.IBqlSelect)
				         || constructedFromSymbol.ImplementsInterface(Context.IBqlSearch)) // TODO: could be Coalesce - handle this case in the future
				{
					withIndent = true;
				}
				else if (constructedFromSymbol.ImplementsInterface(Context.IBqlJoin)
				    || constructedFromSymbol.ImplementsInterface(Context.IBqlWhere)
				    || constructedFromSymbol.ImplementsInterface(Context.IBqlOrderBy))
				{
					Set(node, NewLineAndIndentation(node));
					withIndent = true;
				}
				else if (constructedFromSymbol.ImplementsInterface(Context.IBqlSortColumn))
				{
					Set(node, NewLineAndIndentation(node));
				}
			}
			
			if (withIndent) IncreaseCurrentIndentation();
			base.VisitGenericName(node);
			if (withIndent) DecreaseCurrentIndentation();
		}

		public override void VisitIdentifierName(IdentifierNameSyntax node)
		{
			SyntaxNode genericNameNode = node.Parent?.Parent; // IdentifierNameSyntax -> TypeArgumentSyntaxList -> GenericNameSyntax
			if (genericNameNode != null)
			{
				INamedTypeSymbol parentType = GetTypeSymbol(genericNameNode);
				INamedTypeSymbol constructedFromSymbol = parentType?.ConstructedFrom; // get generic type

				if (constructedFromSymbol != null)
				{
					if (constructedFromSymbol.InheritsFromOrEquals(Context.PXSelectBase)
					    || constructedFromSymbol.ImplementsInterface(Context.IBqlSelect)
					    || constructedFromSymbol.ImplementsInterface(Context.IBqlSearch)) // TODO: could be Coalesce - handle this case in the future
					{
						Set(node, NewLineAndIndentation(node));
					}
				}
			}

			base.VisitIdentifierName(node);
		}

		public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
		{
			// Move BQL View field name to a new line and indent it
			var parentNode = node.Parent as VariableDeclarationSyntax;
			var genericNameNode = parentNode?.Type as GenericNameSyntax;

			if (genericNameNode != null)
			{
				INamedTypeSymbol parentType = GetTypeSymbol(genericNameNode);
				INamedTypeSymbol constructedFromSymbol = parentType?.ConstructedFrom; // get generic type

				if (constructedFromSymbol != null
					&& constructedFromSymbol.InheritsFromOrEquals(Context.PXSelectBase))
				{
					IncreaseCurrentIndentation();
					Set(node, NewLineAndIndentation(node));
					DecreaseCurrentIndentation();
				}
			}

			base.VisitVariableDeclarator(node);
		}

		private SyntaxTriviaList NewLineAndIndentation(SyntaxNode node)
		{
			SyntaxTriviaList defaultTrivia = GetDefaultLeadingTrivia(node);
			SyntaxTriviaList currentTrivia = GetCurrentIndentationTrivia(node);
			SyntaxTriviaList newTrivia = EndOfLineTrivia.AddRange(defaultTrivia.AddRange(currentTrivia));
			
			return newTrivia;
		}

		private SyntaxTriviaList GetDefaultLeadingTrivia(SyntaxNode node)
		{
			if (node == null) return SyntaxTriviaList.Empty;
			if (node.HasLeadingTrivia &&
				(node.IsKind(SyntaxKind.FieldDeclaration) // View
				|| node.IsKind(SyntaxKind.AttributeList) // BQL in attribute
				|| node.IsKind(SyntaxKind.SimpleMemberAccessExpression))) // Static call
			{
				return node.GetLeadingTrivia();
			}

			return GetDefaultLeadingTrivia(node.Parent);
		}

		private SyntaxTriviaList GetCurrentIndentationTrivia(SyntaxNode node)
		{
			if (node == null) return SyntaxTriviaList.Empty;
			
			SyntaxTriviaList result = SyntaxTriviaList.Empty;
			foreach (SyntaxTriviaList trivia in _currentIndentation)
			{
				result = result.AddRange(trivia);
			}
			return result;
		}

		private void IncreaseCurrentIndentation()
		{
			_currentIndentation.Push(IndentationTrivia);
		}

		private void DecreaseCurrentIndentation()
		{
			_currentIndentation.Pop();
		}
	}
}
