﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Acuminator.Analyzers.StaticAnalysis.LegacyBqlField
{
	[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
	public class LegacyBqlFieldFix : CodeFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds { get; } =
			ImmutableArray.Create(Descriptors.PX1060_LegacyBqlField.Id);

		public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		public override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

			var classNode = root.FindNode(context.Span).FirstAncestorOrSelf<ClassDeclarationSyntax>();
			if (classNode == null)
				return;

			var diagnostic = context.Diagnostics.FirstOrDefault(d => d.Id == Descriptors.PX1060_LegacyBqlField.Id);
			if (diagnostic == null)
				return;

			if (diagnostic.Properties == null
				|| !diagnostic.Properties.TryGetValue(LegacyBqlFieldAnalyzer.CorrespondingPropertyType, out string typeName)
				|| String.IsNullOrEmpty(typeName))
			{
				return;
			}

			context.CancellationToken.ThrowIfCancellationRequested();

			TypeSyntax newBaseType = CreateBaseType(typeName, classNode.Identifier.Text);

			if (newBaseType != null)
			{
				string title = nameof(Resources.PX1060Fix).GetLocalized().ToString();
				context.RegisterCodeFix(
					CodeAction.Create(
						title,
						c => Task.Run(() =>
							context.Document.WithSyntaxRoot(
								root.ReplaceNode(
									classNode.BaseList,
									BaseList(
										SeparatedList(
											new BaseTypeSyntax[]
											{
												SimpleBaseType(newBaseType)
											})))), c),
						title),
					context.Diagnostics); 
			}
		}

		private TypeSyntax CreateBaseType(string typeName, string dacFieldName)
		{
			if (!PropertyTypeToFieldType.ContainsKey(typeName))
				return null;

			return IdentifierName($"PX.Data.BQL.Bql{PropertyTypeToFieldType[typeName]}.Field<{dacFieldName}>");
		}

		public static readonly IReadOnlyDictionary<string, string> PropertyTypeToFieldType = new Dictionary<string, string>
		{
			["String"] = "String",
			["Guid"] = "Guid",
			["DateTime"] = "DateTime",
			["Boolean"] = "Bool",
			["Byte"] = "Byte",
			["Int16"] = "Short",
			["Int32"] = "Int",
			["Int64"] = "Long",
			["Single"] = "Float",
			["Double"] = "Double",
			["Decimal"] = "Decimal",
			["Byte[]"] = "ByteArray",
		};
	}
}