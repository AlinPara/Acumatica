﻿
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Acuminator.Utilities.Common;
using Acuminator.Utilities.Roslyn.Constants;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;

namespace Acuminator.Analyzers.StaticAnalysis.DacExtensionDefaultAttribute
{
	[Shared]
	[ExportCodeFixProvider(LanguageNames.CSharp)]
	public class DacExtensionDefaultAttributeFix : PXCodeFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds { get; } =
			ImmutableArray.Create(
				Descriptors.PX1030_DefaultAttibuteToExistingRecordsError.Id,
				Descriptors.PX1030_DefaultAttibuteToExistingRecordsWarning.Id,
				Descriptors.PX1030_DefaultAttibuteToExistingRecordsOnDAC.Id);

		protected override async Task RegisterCodeFixesForDiagnosticAsync(CodeFixContext context, Diagnostic diagnostic)
		{
			SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			SyntaxNode? codeFixNode = root?.FindNode(context.Span);
			AttributeSyntax? attributeNode = codeFixNode as AttributeSyntax;

			if (attributeNode?.Name is IdentifierNameSyntax identifierNode && identifierNode.Identifier.Text.Equals(TypeNames.PXDefault))
			{
				bool isBoundField = IsBoundField(diagnostic);
				string codeActionNameBound = nameof(Resources.PX1030FixBound).GetLocalized().ToString();
				string codeActionNameUnbound = nameof(Resources.PX1030FixUnbound).GetLocalized().ToString();

				CodeAction codeActionBound =
					CodeAction.Create(codeActionNameBound,
							cToken => AddToAttributePersistingCheckNothing(context.Document, context.Span, isBoundField, cToken),
							equivalenceKey: codeActionNameBound);
				context.RegisterCodeFix(codeActionBound, diagnostic);


				if (!isBoundField)
				{
					CodeAction codeActionUnbound =
						CodeAction.Create(codeActionNameUnbound,
							cToken => ReplaceAttributeToPXUnboundDefault(context.Document, context.Span, cToken),
							equivalenceKey: codeActionNameUnbound);
					context.RegisterCodeFix(codeActionUnbound, diagnostic);
				}

			}

			return;
		}

		private async Task<Document> ReplaceAttributeToPXUnboundDefault(Document document, TextSpan span, CancellationToken cancellationToken)
		{
			SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

			if (root?.FindNode(span) is not AttributeSyntax attributeNode || attributeNode.Parent is not AttributeListSyntax)
				return document;

			cancellationToken.ThrowIfCancellationRequested();

			SyntaxGenerator generator = SyntaxGenerator.GetGenerator(document);

			var pxUnboundDefaultAttribute = generator.Attribute(TypeNames.PXUnboundDefault) as AttributeListSyntax;

			if (pxUnboundDefaultAttribute == null)
				return document;

			SyntaxNode modifiedRoot = root.ReplaceNode(attributeNode, pxUnboundDefaultAttribute.Attributes[0]);
			return document.WithSyntaxRoot(modifiedRoot);
		}

		private async Task<Document> AddToAttributePersistingCheckNothing(Document document, TextSpan span, bool isBoundField, CancellationToken cancellationToken)
		{
			SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

			if (root?.FindNode(span) is not AttributeSyntax attributeNode || attributeNode.Parent is not AttributeListSyntax attributeList)
				return document;

			cancellationToken.ThrowIfCancellationRequested();

			SyntaxGenerator generator = SyntaxGenerator.GetGenerator(document);

			var memberAccessExpression = generator.MemberAccessExpression(generator.IdentifierName(TypeNames.PXPersistingCheck),
																		  generator.IdentifierName(TypeNames.PersistingCheckNothing));
			var persistingAttributeArgument = generator.AttributeArgument(TypeNames.PersistingCheck,
																		  memberAccessExpression) as AttributeArgumentSyntax;
			SyntaxNode? modifiedRoot = null;

			if (attributeNode.ArgumentList != null)
			{
				AttributeArgumentSyntax? argument = GetArgumentFromAttribute();

				if (argument != null)
				{
					persistingAttributeArgument = argument.ReplaceNode(argument.Expression, memberAccessExpression);
					var newAttributeNode = attributeNode.ReplaceNode(argument, persistingAttributeArgument);
					var newAttributeList = attributeList.ReplaceNode(attributeNode, newAttributeNode);
					modifiedRoot = root.ReplaceNode(attributeList, newAttributeList);
				}
				else
				{
					var newAttributeList = generator.AddAttributeArguments(attributeNode, [persistingAttributeArgument]) as AttributeListSyntax;

					if (newAttributeList != null)
						modifiedRoot = root.ReplaceNode(attributeNode, newAttributeList.Attributes[0]);
				}
			}
			else
			{
				var newAttribute = generator.InsertAttributeArguments(attributeNode, 1, [persistingAttributeArgument]) as AttributeListSyntax;

				if (newAttribute != null)
					modifiedRoot = root.ReplaceNode(attributeNode, newAttribute.Attributes[0]);
			}

			return modifiedRoot != null
				? document.WithSyntaxRoot(modifiedRoot)
				: document;


			//-------------------------------------------------Local Function----------------------------------------------------------
			AttributeArgumentSyntax? GetArgumentFromAttribute()
			{
				foreach (AttributeArgumentSyntax _argument in attributeNode.ArgumentList.Arguments)
				{
					if (_argument.NameEquals != null
						&& _argument.NameEquals.Name.Identifier.Text.Contains(TypeNames.PersistingCheck))
					{
						return _argument;
					}
				}

				return null;
			}
		}

		public static bool IsBoundField(Diagnostic diagnostic) =>
			diagnostic.IsFlagSet(DiagnosticProperty.IsBoundField);
	}

}