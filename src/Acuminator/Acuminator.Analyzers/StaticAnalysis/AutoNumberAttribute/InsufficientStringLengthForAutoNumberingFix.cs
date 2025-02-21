﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;

using Acuminator.Utilities.Common;
using Acuminator.Utilities.Roslyn.Semantic;
using Acuminator.Utilities.Roslyn.Syntax;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Acuminator.Analyzers.StaticAnalysis.AutoNumberAttribute
{
	[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
	public class InsufficientStringLengthForAutoNumberingFix : PXCodeFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds { get; } = 
			ImmutableArray.Create(Descriptors.PX1020_InsufficientStringLengthForDacPropertyWithAutoNumbering.Id);

		protected override async Task RegisterCodeFixesForDiagnosticAsync(CodeFixContext context, Diagnostic diagnostic)
		{
			SemanticModel? semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken)
																 .ConfigureAwait(false);
			if (semanticModel == null)
				return;

			PXContext pxContext = new PXContext(semanticModel.Compilation, codeAnalysisSettings: null);
			int minLengthForAutoNumbering = pxContext.AttributeTypes.AutoNumberAttribute.MinAutoNumberLength;

			string codeActionName = nameof(Resources.PX1020Fix).GetLocalized().ToString();
			CodeAction codeAction = CodeAction.Create(codeActionName,
													  cToken => MakeStringLengthSufficientForAutoNumberingAsync(context.Document, context.Span, minLengthForAutoNumbering, cToken),
													  equivalenceKey: codeActionName);

			context.RegisterCodeFix(codeAction, diagnostic);
		}

		private async Task<Document> MakeStringLengthSufficientForAutoNumberingAsync(Document document, TextSpan diagnosticSpan, 
																					 int minLengthForAutoNumbering, CancellationToken cancellationToken)
		{
			SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			AttributeArgumentSyntax? attributeArgument = GetAttributeArgumentNodeToBeReplaced(root, diagnosticSpan);

			if (attributeArgument == null || cancellationToken.IsCancellationRequested)
				return document;
		
			AttributeArgumentSyntax modifiedArgument =
				attributeArgument.WithExpression(
					SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,
													SyntaxFactory.Literal(minLengthForAutoNumbering)));
			if (modifiedArgument == null)
				return document;

			var modifiedRoot = root!.ReplaceNode(attributeArgument, modifiedArgument);
			return document.WithSyntaxRoot(modifiedRoot);
		}		

		private AttributeArgumentSyntax? GetAttributeArgumentNodeToBeReplaced(SyntaxNode? root, TextSpan diagnosticSpan)
		{
			SyntaxNode? node = root?.FindNode(diagnosticSpan);

			return node switch
			{
				LiteralExpressionSyntax literal 
				when literal.Kind() == SyntaxKind.NumericLiteralExpression && 
					 literal.Token.Value is int									=> literal.Parent<AttributeArgumentSyntax>(),
				IdentifierNameSyntax namedConstant                              => namedConstant.Parent<AttributeArgumentSyntax>(),
				MemberAccessExpressionSyntax memberAccess                       => memberAccess.Parent<AttributeArgumentSyntax>(),
				AttributeArgumentSyntax attributeArgument                       => attributeArgument,
				AttributeSyntax attribute                                       => SearchForAttributeArgumentToBeReplaced(attribute),
				_                                                               => null,
			};
		}

		private AttributeArgumentSyntax? SearchForAttributeArgumentToBeReplaced(AttributeSyntax attribute)
		{
			if (attribute.ArgumentList == null)
				return null;

			var arguments = attribute.ArgumentList.Arguments;
			var candidateAttributes = new List<AttributeArgumentSyntax>(capacity: 1);

			for (int i = 0; i < arguments.Count; i++)
			{
				AttributeArgumentSyntax attributeArgument = arguments[i];

				if (attributeArgument.NameEquals != null)
					continue;

				switch (attributeArgument.Expression)
				{
					case IdentifierNameSyntax _:
					case MemberAccessExpressionSyntax _:
					case LiteralExpressionSyntax literal 
					when literal.Kind() == SyntaxKind.NumericLiteralExpression && literal.Token.Value is int:
						candidateAttributes.Add(attributeArgument);
						continue;
				}
			}

			return candidateAttributes.Count == 1
				? candidateAttributes[0]
				: null;
		}
	}
}
