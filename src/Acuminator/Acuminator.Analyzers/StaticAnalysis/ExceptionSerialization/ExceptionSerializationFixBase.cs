﻿
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Acuminator.Utilities.Roslyn.Semantic;
using Acuminator.Utilities.Roslyn.Syntax;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Acuminator.Analyzers.StaticAnalysis.ExceptionSerialization
{
	public abstract class ExceptionSerializationFixBase : PXCodeFixProvider
	{
		protected const string SerializationInfoParameterName = "info";
		protected const string StreamingContextParameterName = "context";

		protected override Task RegisterCodeFixesForDiagnosticAsync(CodeFixContext context, Diagnostic diagnostic)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			string addSerializationMemberTitle = GetCodeFixTitle(diagnostic);
			var addSerializationMemberCodeAction =
					CodeAction.Create(addSerializationMemberTitle,
									  cToken => AddMissingExceptionMembersForCorrectSerializationAsync(context.Document, context.Span, diagnostic, cToken),
									  equivalenceKey: addSerializationMemberTitle);

			context.CancellationToken.ThrowIfCancellationRequested();
			context.RegisterCodeFix(addSerializationMemberCodeAction, diagnostic);

			return Task.CompletedTask;
		}

		protected abstract string GetCodeFixTitle(Diagnostic diagnostic);

		protected virtual async Task<Document> AddMissingExceptionMembersForCorrectSerializationAsync(Document document, TextSpan span, Diagnostic diagnostic,
																									  CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			var exceptionDeclaration = root?.FindNode(span)
										   ?.ParentOrSelf<ClassDeclarationSyntax>();

			if (exceptionDeclaration == null)
				return document;

			SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

			if (semanticModel == null)
				return document;

			var pxContext = new PXContext(semanticModel.Compilation, codeAnalysisSettings: null);
			SyntaxGenerator generator = SyntaxGenerator.GetGenerator(document);
			var generatedSerializationMemberDeclaration = GenerateSerializationMemberNode(generator, exceptionDeclaration, pxContext, diagnostic);

			if (generatedSerializationMemberDeclaration == null)
				return document;

			cancellationToken.ThrowIfCancellationRequested();

			int positionToInsert = FindPositionToInsertGeneratedMember(exceptionDeclaration, semanticModel, pxContext, cancellationToken);
			ClassDeclarationSyntax? modifiedExceptionDeclaration = positionToInsert >= 0
				? generator.InsertMembers(exceptionDeclaration, positionToInsert, generatedSerializationMemberDeclaration) as ClassDeclarationSyntax
				: generator.AddMembers(exceptionDeclaration, generatedSerializationMemberDeclaration) as ClassDeclarationSyntax;

			if (modifiedExceptionDeclaration == null)
				return document;

			cancellationToken.ThrowIfCancellationRequested();
			var changedRoot = root!.ReplaceNode(exceptionDeclaration, modifiedExceptionDeclaration) as CompilationUnitSyntax;

			if (changedRoot == null)
				return document;

			changedRoot = AddMissingUsingDirectives(changedRoot, diagnostic);
			return document.WithSyntaxRoot(changedRoot);
		}

		/// <summary>
		/// Searches for the position to insert the generated exception type member responsible for the serialization.
		/// </summary>
		/// <remarks>
		/// We try to keep a preferred order of serialization type members: serialization constructor folowed by the GetObjectData method override.
		/// </remarks>
		/// <param name="exceptionDeclaration">The exception declaration.</param>
		/// <param name="semanticModel">The semantic model.</param>
		/// <param name="pxContext">The Acumatica context.</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>
		/// The found position to insert the generated type member. <c>-1</c> if no preferrable position is found (member will be added as the last type member).
		/// </returns>
		protected abstract int FindPositionToInsertGeneratedMember(ClassDeclarationSyntax exceptionDeclaration, SemanticModel semanticModel,
																   PXContext pxContext, CancellationToken cancellationToken);

		protected abstract MemberDeclarationSyntax? GenerateSerializationMemberNode(SyntaxGenerator generator, ClassDeclarationSyntax exceptionDeclaration,
																					PXContext pxContext, Diagnostic diagnostic);

		protected bool IsMethodUsedForSerialization(IMethodSymbol method, PXContext pxContext) =>
			method.Parameters.Length == 2 &&
			pxContext.Serialization.SerializationInfo.Equals(method.Parameters[0]?.Type, SymbolEqualityComparer.Default)  &&
			pxContext.Serialization.StreamingContext.Equals(method.Parameters[1]?.Type, SymbolEqualityComparer.Default);

		protected SyntaxNode[] GenerateSerializationMemberParameters(SyntaxGenerator generator, PXContext pxContext) =>
			new[]
			{
				generator.ParameterDeclaration(name: SerializationInfoParameterName,
											   type: generator.IdentifierName(pxContext.Serialization.SerializationInfo.Name)),
				generator.ParameterDeclaration(name: StreamingContextParameterName,
											   type: generator.IdentifierName(pxContext.Serialization.StreamingContext.Name))
			};

		protected SyntaxNode GenerateReflectionSerializerMethodCall(SyntaxGenerator generator, string methodName, PXContext pxContext)
		{
			SyntaxNode[] reflectionSerializerCallArguments =
			{
				generator.Argument(
					generator.ThisExpression()),
				generator.Argument(
					IdentifierName(SerializationInfoParameterName))
			};

			// In older version of Acumatica there is only PXReflectionSerializer
			INamedTypeSymbol reflectionSerializer = pxContext.Serialization.ReflectionSerializer ??
													pxContext.Serialization.PXReflectionSerializer;
			return generator.ExpressionStatement
					(
						generator.InvocationExpression(
							generator.MemberAccessExpression(
								generator.IdentifierName(reflectionSerializer.Name),
								methodName),
							reflectionSerializerCallArguments)
					);
		}

		protected abstract CompilationUnitSyntax AddMissingUsingDirectives(CompilationUnitSyntax root, Diagnostic diagnostic);
	}
}
