﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;

namespace Acuminator.Analyzers.FixProviders
{
	[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
	public class CorrectPXActionSignatureFix : CodeFixProvider
	{
		private class ChangeSignatureAction : CodeAction
		{
			private readonly string _title;
			private readonly Document _document;
			private readonly MethodDeclarationSyntax _method;

			public override string Title => _title;
			public override string EquivalenceKey => _title;

			protected override async Task<Document> GetChangedDocumentAsync(CancellationToken cancellationToken)
			{
				var semanticModel = await _document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
				var pxContext = new PXContext(semanticModel.Compilation);

				var oldRoot = await _document.GetSyntaxRootAsync(cancellationToken);
				var newRoot = oldRoot;
				var generator = SyntaxGenerator.GetGenerator(_document);
				var IEnumerableType = (TypeSyntax) generator.TypeExpression(semanticModel.Compilation.GetSpecialType(SpecialType.System_Collections_IEnumerable));
				var PXAdapterType = generator.TypeExpression(pxContext.PXAdapterType);

				var adapterPar = (ParameterSyntax) generator.ParameterDeclaration("adapter", PXAdapterType);
				var oldParameters = _method.ParameterList;
				var newParameters = SyntaxFactory.SeparatedList<ParameterSyntax>(new[] { adapterPar });
				var newMethod = _method.WithReturnType(IEnumerableType);
				newMethod = newMethod.WithParameterList(oldParameters.WithParameters(newParameters));

				var controlFlow = semanticModel.AnalyzeControlFlow(_method.Body);
				if (controlFlow.Succeeded && controlFlow.ReturnStatements.IsEmpty)
				{
					newMethod = newMethod.AddBodyStatements((StatementSyntax) generator.ReturnStatement(
						generator.InvocationExpression(
							generator.MemberAccessExpression(
								generator.IdentifierName("adapter"), 
								"Get")))
								.WithAdditionalAnnotations(Formatter.Annotation));
				}

				newRoot = newRoot.ReplaceNode(_method, newMethod);
				newRoot = AddCollectionsUsing(newRoot, generator);

				return _document.WithSyntaxRoot(newRoot);
			}

			private SyntaxNode AddCollectionsUsing(SyntaxNode root, SyntaxGenerator generator)
			{
				var oldUsings = ((CompilationUnitSyntax) root).Usings;
				var usingCollections = (UsingDirectiveSyntax) generator.NamespaceImportDeclaration(typeof(IEnumerable).Namespace);
				bool usingFound = false;
				bool usingAdded = false;
				var newUsingsList = new List<SyntaxNode>();

				foreach (var node in oldUsings)
				{
					if (!usingAdded && String.CompareOrdinal(node.Name.ToString(), usingCollections.Name.ToString()) > 0)
					{
						newUsingsList.Add(usingCollections);
						usingAdded = true;
					}
					newUsingsList.Add(node);

					if (SyntaxFactory.AreEquivalent(node, usingCollections))
					{
						usingFound = true;
					}
				}

				if (!usingFound)
				{
					if (!usingAdded)
					{
						newUsingsList.Add(usingCollections);
					}
					var newUsings = SyntaxFactory.List(newUsingsList);
					return ((CompilationUnitSyntax) root).WithUsings(newUsings);
				}

				return root;
			}

			public ChangeSignatureAction(string title, Document document, MethodDeclarationSyntax method)
			{
				_title = title;
				_document = document;
				_method = method;
			}
		}

		public override ImmutableArray<string> FixableDiagnosticIds { get; } =
			ImmutableArray.Create(Descriptors.PX1000_InvalidPXActionHandlerSignature.Id);

		public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		public override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync().ConfigureAwait(false);
			var method = root.FindNode(context.Span).FirstAncestorOrSelf<MethodDeclarationSyntax>();
			
			context.RegisterCodeFix(new ChangeSignatureAction(nameof(Resources.PX1000Fix).GetLocalized().ToString(), context.Document, method), context.Diagnostics);
		}
	}
}