﻿#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

using Acuminator.Utilities.Common;
using Acuminator.Utilities.Roslyn.Constants;
using Acuminator.Utilities.Roslyn.Semantic.PXGraph;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Acuminator.Utilities.Roslyn.Semantic
{
	public static class ITypeSymbolExtensions
	{
		private const char DefaultGenericArgsCountSeparator = '`';
		private const char DefaultNestedTypesSeparator = '+';

		/// <summary>
		/// Gets the base types and this in this collection. The types are returned from the most derived ones to the most base <see cref="Object"/> type
		/// </summary>
		/// <param name="type">The type to act on.</param>
		/// <returns/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<ITypeSymbol> GetBaseTypesAndThis(this ITypeSymbol type) =>
			type.GetBaseTypesImplementation(includeThis: true);

		/// <summary>
		/// Gets the base types and this in this collection. The types are returned from the most derived ones to the most base <see cref="Object"/> type
		/// </summary>
		/// <param name="type">The type to act on.</param>
		/// <returns/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<ITypeSymbol> GetBaseTypes(this ITypeSymbol type) =>
			type.GetBaseTypesImplementation(includeThis: false);

		private static IEnumerable<ITypeSymbol> GetBaseTypesImplementation(this ITypeSymbol type, bool includeThis)
		{
			type.ThrowOnNull();

			if (type is ITypeParameterSymbol typeParameter)
			{
				// for a type parameter we can consider its generic constraints like "where T : SomeClass" as its base types
				IEnumerable<ITypeSymbol> constraintTypes = typeParameter.GetAllConstraintTypes(includeInterfaces: false)
																	    .SelectMany(constraint => constraint.GetBaseTypesIterator(includeThis: true))
																	    .Distinct<ITypeSymbol>(SymbolEqualityComparer.Default);
				return includeThis 
					? constraintTypes.PrependItem(typeParameter)
					: constraintTypes;
			}

			return type.GetBaseTypesIterator(includeThis);
		}

		private static IEnumerable<ITypeSymbol> GetBaseTypesIterator(this ITypeSymbol typeToUse, bool includeThis)
		{
			var current = includeThis ? typeToUse : typeToUse.BaseType;

			while (current != null)
			{
				yield return current;
				current = current.BaseType;
			}
		}

		public static IEnumerable<INamedTypeSymbol> GetFlattenedNestedTypes(this ITypeSymbol type, CancellationToken cancellationToken)
		{
			type.ThrowOnNull();
			cancellationToken.ThrowIfCancellationRequested();
			return type.GetFlattenedNestedTypesImplementation(shouldWalkThroughNestedTypesPredicate: null, cancellationToken);			
		}

		public static IEnumerable<INamedTypeSymbol> GetFlattenedNestedTypes(this ITypeSymbol type, Func<ITypeSymbol, bool>? shouldWalkThroughNestedTypesPredicate, 
																			CancellationToken cancellationToken)
		{
			type.ThrowOnNull();
			cancellationToken.ThrowIfCancellationRequested();
			shouldWalkThroughNestedTypesPredicate.ThrowOnNull();

			return type.GetFlattenedNestedTypesImplementation(shouldWalkThroughNestedTypesPredicate, cancellationToken);
		}

		private static IEnumerable<INamedTypeSymbol> GetFlattenedNestedTypesImplementation(this ITypeSymbol type, 
																						   Func<ITypeSymbol, bool>? shouldWalkThroughNestedTypesPredicate,
																						   CancellationToken cancellationToken)
		{
			var nestedTypes = type.GetTypeMembers();

			if (nestedTypes.IsDefaultOrEmpty)
				yield break;

			var typesQueue = new Queue<INamedTypeSymbol>(nestedTypes);

			while (typesQueue.Count > 0)
			{
				cancellationToken.ThrowIfCancellationRequested();
				var currentType = typesQueue.Dequeue();
				bool shouldWalkThroughChildNestedTypes = shouldWalkThroughNestedTypesPredicate?.Invoke(currentType) ?? true;

				if (shouldWalkThroughChildNestedTypes)
				{
					var declaredNestedTypes = currentType.GetTypeMembers();

					if (!declaredNestedTypes.IsDefaultOrEmpty)
					{
						foreach (var nestedType in declaredNestedTypes)
						{
							typesQueue.Enqueue(nestedType);
						}
					}
				}

				yield return currentType;
			}
		}

		public static IEnumerable<ITypeSymbol> GetContainingTypesAndThis(this ITypeSymbol type)
		{
			var current = type;

			while (current != null)
			{
				yield return current;
				current = current.ContainingType;
			}
		}

		public static IEnumerable<INamedTypeSymbol> GetContainingTypes(this ISymbol symbol)
		{
			var current = symbol.CheckIfNull().ContainingType;

			while (current != null)
			{
				yield return current;
				current = current.ContainingType;
			}
		}

		public static INamedTypeSymbol? TopMostContainingType(this ITypeSymbol type)
		{
			INamedTypeSymbol current = type.CheckIfNull().ContainingType;

			while (current != null)
			{
				if (current.ContainingType == null)
					return current;

				current = current.ContainingType;
			}

			return null;
		}

		public static IEnumerable<INamespaceSymbol> GetContainingNamespaces(this ITypeSymbol? type)
		{
			var currentNamespace = type?.ContainingNamespace;

			while (currentNamespace != null)
			{
				yield return currentNamespace;
				currentNamespace = currentNamespace.ContainingNamespace;
			}
		}

		/// <summary>
		///  Determine if "type" inherits from "baseType", ignoring constructed types and interfaces, dealing only with original types.
		/// </summary>
		/// <param name="type">The type to act on.</param>
		/// <param name="baseType">The base type.</param>
		/// <returns/>    
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool InheritsFromOrEquals(this ITypeSymbol type, ITypeSymbol baseType) =>
			InheritsFromOrEquals(type, baseType, includeInterfaces: false);

		/// <summary>
		/// Determine if "type" inherits from "baseType", ignoring constructed types, optionally including interfaces, dealing only with original types.
		/// </summary>
		/// <param name="type">The type to act on.</param>
		/// <param name="baseType">The base type.</param>
		/// <param name="includeInterfaces">True to include, false to exclude the interfaces.</param>
		/// <returns/>
		public static bool InheritsFromOrEquals(this ITypeSymbol type, ITypeSymbol baseType, bool includeInterfaces)
		{
			type.ThrowOnNull();
			baseType.ThrowOnNull();

			var typeList = type.GetBaseTypesAndThis();

			if (includeInterfaces)
			{
				typeList = typeList.ConcatStructList(type.AllInterfaces);
			}

			return typeList.Any(t => t.Equals(baseType, SymbolEqualityComparer.Default));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool InheritsFromOrEqualsGeneric(this ITypeSymbol type, ITypeSymbol baseType) =>
			InheritsFromOrEqualsGeneric(type, baseType, includeInterfaces: false);

		public static bool InheritsFromOrEqualsGeneric(this ITypeSymbol type, ITypeSymbol baseType, bool includeInterfaces)
		{
			type.ThrowOnNull();
			baseType.ThrowOnNull();

			var typeList = type.GetBaseTypesAndThis();

			if (includeInterfaces)
				typeList = typeList.ConcatStructList(type.AllInterfaces);

			return typeList.Select(t => t.OriginalDefinition)
						   .Any(t => t.Equals(baseType.OriginalDefinition, SymbolEqualityComparer.Default));
		}

		public static bool InheritsFrom(this ITypeSymbol type, ITypeSymbol baseType, bool includeInterfaces = false)
		{
			type.ThrowOnNull();
			baseType.ThrowOnNull();

			IEnumerable<ITypeSymbol> baseTypes = type.GetBaseTypes();

			if (includeInterfaces)
			{
				baseTypes = baseTypes.ConcatStructList(type.AllInterfaces);
			}
			
			return baseTypes.Any(t => t.Equals(baseType, SymbolEqualityComparer.Default));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool ImplementsInterface(this ITypeSymbol type, ITypeSymbol interfaceType)
		{
			type.ThrowOnNull();
			interfaceType.ThrowOnNull();

			if (interfaceType.TypeKind != TypeKind.Interface)
			{
				throw new ArgumentException("Invalid interface type", nameof(interfaceType));
			}

			// Interface types themselves are not included into AllInterfaces set, they do not implement themselves from Roslyn POV.
			// However, for simplicity in Acuminator analysis we can assume equality of type and interfaceType as a special case of type implementing interfaceType interface.
			// Therefore, we need to check type if it's an interface if it equals to the interfaceType
			if (type.TypeKind == TypeKind.Interface && type.Equals(interfaceType, SymbolEqualityComparer.Default))
				return true;

			return type.AllInterfaces.Any(t => t.Equals(interfaceType, SymbolEqualityComparer.Default));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool InheritsFrom(this ITypeSymbol symbol, string baseType)
		{
			symbol.ThrowOnNull();
			baseType.ThrowOnNullOrWhiteSpace();

			return symbol.GetBaseTypesAndThis()
						 .Any(t => t.Name == baseType);
		}
		
		/// <summary>
		/// Determine if "type" inherits from "baseType", ignoring constructed types, optionally including interfaces, dealing only with original
		/// types.
		/// </summary>
		/// <param name="type">The type to act on.</param>
		/// <param name="baseTypeName">Name of the base type.</param>
		/// <param name="includeInterfaces">(Optional) True to include, false to exclude the interfaces.</param>
		/// <returns/>
		public static bool InheritsOrImplementsOrEquals(this ITypeSymbol? type, string baseTypeName,
														bool includeInterfaces = true)
		{
			if (type == null)
				return false;

			IEnumerable<ITypeSymbol> baseTypes = type.GetBaseTypesAndThis();

			if (includeInterfaces)
			{
				baseTypes = baseTypes.ConcatStructList(type.AllInterfaces);
			}

			return baseTypes.Any(typeSymbol => typeSymbol.Name == baseTypeName);					
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool ImplementsInterface(this ITypeSymbol? type, string interfaceName)
		{
			if (type == null)
				return false;
			else if (type.TypeKind == TypeKind.Interface && type.Name == interfaceName)
				return true;
			else
				return type.AllInterfaces.Any(interfaceType => interfaceType.Name == interfaceName);
		}
			

		/// <summary>
		/// Gets <paramref name="typeParameterSymbol"/> and its all constraint types.
		/// </summary>
		/// <param name="typeParameterSymbol">The typeParameterSymbol to act on.</param>
		/// <param name="includeInterfaces">(Optional) True to include, false to exclude the interfaces.</param>
		/// <returns/>
		public static IEnumerable<ITypeSymbol> GetTypeWithAllConstraintTypes(this ITypeParameterSymbol typeParameterSymbol,
																			 bool includeInterfaces = true)
		{
			var constraintTypes = typeParameterSymbol.GetAllConstraintTypes(includeInterfaces);
			return constraintTypes.PrependItem(typeParameterSymbol);
		}

		/// <summary>
		/// Gets all constraint types for the given <paramref name="typeParameterSymbol"/>.
		/// </summary>
		/// <param name="typeParameterSymbol">The typeParameterSymbol to act on.</param>
		/// <param name="includeInterfaces">(Optional) True to include, false to exclude the interfaces.</param>
		/// <returns/>
		public static IEnumerable<ITypeSymbol> GetAllConstraintTypes(this ITypeParameterSymbol typeParameterSymbol, bool includeInterfaces = true)
		{
			typeParameterSymbol.ThrowOnNull();
			
			var constraintTypes = includeInterfaces
				? GetAllConstraintTypesImplementation(typeParameterSymbol)
				: GetAllConstraintTypesImplementation(typeParameterSymbol)
							.Where(type => type.TypeKind != TypeKind.Interface);

			return constraintTypes.Distinct<ITypeSymbol>(SymbolEqualityComparer.Default);

			//---------------------------------Local Functions--------------------------------------------------------
			IEnumerable<ITypeSymbol> GetAllConstraintTypesImplementation(ITypeParameterSymbol typeParameter, int recursionLevel = 0)
			{
				const int maxRecursionLevel = 40;

				if (recursionLevel > maxRecursionLevel || typeParameter.ConstraintTypes.Length == 0)
					yield break;

				foreach (ITypeSymbol constraintType in typeParameter.ConstraintTypes)
				{
					if (constraintType is ITypeParameterSymbol constraintTypeParameter)
					{
						var nextOrderTypeParams = GetAllConstraintTypesImplementation(constraintTypeParameter, recursionLevel + 1);

						foreach (ITypeSymbol type in nextOrderTypeParams)
						{
							yield return type;
						}
					}
					else
					{
						yield return constraintType;
					}
				}
			}
		}

		/// <summary>
		/// Gets the depth of inheritance between <paramref name="type"/> and its <paramref name="baseType"/>.
		/// If <paramref name="baseType"/> is not an ancestor of type returns <c>null</c>.
		/// </summary>
		/// <param name="type">The type to act on.</param>
		/// <param name="baseType">The base type.</param>
		/// <returns>
		/// The inheritance depth.
		/// </returns>
		public static int? GetInheritanceDepth(this ITypeSymbol type, ITypeSymbol baseType)
		{
			type.ThrowOnNull();
			baseType.ThrowOnNull();

			ITypeSymbol? current = type;
			int depth = 0;

			while (current != null && !current.Equals(baseType, SymbolEqualityComparer.Default))
			{
				current = current.BaseType;
				depth++;
			}

			return current != null ? depth : null;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<ITypeSymbol> GetAllAttributesDefinedOnThisAndBaseTypes(this ITypeSymbol typeSymbol) =>
			typeSymbol.GetAllAttributesApplicationsDefinedOnThisAndBaseTypes()
					  .Select(a => a.AttributeClass)
					  .Where(attrType => attrType != null)!;
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<AttributeData> GetAllAttributesApplicationsDefinedOnThisAndBaseTypes(this ITypeSymbol typeSymbol)
		{
			typeSymbol.ThrowOnNull();
			return typeSymbol.GetBaseTypesAndThis()
							 .SelectMany(t => t.GetAttributes());
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ITypeSymbol? GetUnderlyingTypeFromNullable(this ITypeSymbol? typeSymbol, PXContext pxContext)
		{
			if (!typeSymbol.IsNullable(pxContext) || typeSymbol is not INamedTypeSymbol namedTypeSymbol)
				return null;

			ImmutableArray<ITypeSymbol> typeArgs = namedTypeSymbol.TypeArguments;
			return typeArgs.Length == 1
				? typeArgs[0]
				: null;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsNullable(this ITypeSymbol? typeSymbol, PXContext pxContext)
		{
			pxContext.ThrowOnNull();
			return typeSymbol?.OriginalDefinition?.Equals(pxContext.SystemTypes.Nullable, SymbolEqualityComparer.Default) ?? false;
		}

		/// <summary>
		/// An INamedTypeSymbol extension method that gets CLR-style full type name from type.
		/// </summary>
		/// <param name="typeSymbol">The typeSymbol to act on.</param>
		/// <returns/>
		public static string GetCLRTypeNameFromType(this ITypeSymbol? typeSymbol)
		{
			if (typeSymbol == null)
				return string.Empty;
			else if (typeSymbol.ContainingType == null)
				return typeSymbol.GetClrStyleTypeFullNameForNotNestedType();

			Stack<ITypeSymbol> containingTypesStack = typeSymbol.GetContainingTypesAndThis().ToStack();
			string notNestedTypeName = containingTypesStack.Pop().GetClrStyleTypeFullNameForNotNestedType();
			StringBuilder nameBuilder = new StringBuilder(notNestedTypeName, capacity: 128);

			while (containingTypesStack.Count > 0)
			{
				ITypeSymbol nestedType = containingTypesStack.Pop();
				nameBuilder.AppendClrStyleNestedTypeShortName(nestedType);
			}

			return nameBuilder.ToString();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static StringBuilder AppendClrStyleNestedTypeShortName(this StringBuilder builder, ITypeSymbol typeSymbol)
		{
			builder.Append(DefaultNestedTypesSeparator)
				   .Append(typeSymbol.Name);

			if (typeSymbol is not INamedTypeSymbol namedType || !namedType.IsGenericType)
				return builder;

			var typeArgs = namedType.TypeArguments;
			return builder.Append(DefaultGenericArgsCountSeparator)
						  .Append(typeArgs.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static string GetClrStyleTypeFullNameForNotNestedType(this ITypeSymbol notNestedTypeSymbol)
		{
			if (notNestedTypeSymbol is not INamedTypeSymbol namedType || !namedType.IsGenericType)
				return notNestedTypeSymbol.ToDisplayString();

			var typeArgs = namedType.TypeArguments;
			var displayFormat = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
														genericsOptions: SymbolDisplayGenericsOptions.None);
			string typeNameWithoutGeneric = namedType.ToDisplayString(displayFormat);
			return typeNameWithoutGeneric + DefaultGenericArgsCountSeparator + typeArgs.Length;
		}

		internal static IEnumerable<(ConstructorDeclarationSyntax Node, IMethodSymbol Symbol)> GetDeclaredInstanceConstructors(
			this INamedTypeSymbol typeSymbol, CancellationToken cancellation = default)
		{
			typeSymbol.ThrowOnNull();

			List<(ConstructorDeclarationSyntax, IMethodSymbol)> initializers = new List<(ConstructorDeclarationSyntax, IMethodSymbol)>();

			foreach (IMethodSymbol ctr in typeSymbol.InstanceConstructors)
			{
				cancellation.ThrowIfCancellationRequested();

				if (!ctr.IsDefinition)
					continue;

				SyntaxReference? reference = ctr.DeclaringSyntaxReferences.FirstOrDefault();
				if (reference == null)
					continue;

				if (reference.GetSyntax(cancellation) is not ConstructorDeclarationSyntax node)
					continue;

				initializers.Add((node, ctr));
			}

			return initializers;
		}

		public static ImmutableArray<StaticConstructorInfo> GetStaticConstructors(this INamedTypeSymbol typeSymbol,
																				  CancellationToken cancellation = default)
		{
			typeSymbol.ThrowOnNull();

			int order = 0;
			List<StaticConstructorInfo> staticCtrs = new List<StaticConstructorInfo>();

			foreach (IMethodSymbol ctr in typeSymbol.StaticConstructors)
			{
				cancellation.ThrowIfCancellationRequested();

				SyntaxReference? reference = ctr.DeclaringSyntaxReferences.FirstOrDefault();

				if (reference?.GetSyntax(cancellation) is not ConstructorDeclarationSyntax node)
					continue;

				staticCtrs.Add(new StaticConstructorInfo(node, ctr, order));
				order++;
			}

			return staticCtrs.ToImmutableArray();
		}

		/// <summary>
		/// Get all methods of this <paramref name="type"/>. If <paramref name="methodName"/> is specified then all methods with this name are returned.
		/// </summary>
		/// <param name="type">The type to act on.</param>
		/// <param name="methodName">(Optional) Name of the method to look for.</param>
		/// <returns>
		/// Returns a collection containing all the methods of this symbol. Never returns Null.
		/// </returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<IMethodSymbol> GetMethods(this ITypeSymbol type, string? methodName = null) =>
			GetMembersInternal<IMethodSymbol>(type, methodName);

		/// <summary>
		/// Get all properties of this <paramref name="type"/>. If <paramref name="propertyName"/> is specified then all properties with this name are returned.
		/// </summary>
		/// <param name="type">The type to act on.</param>
		/// <param name="propertyName">(Optional) Name of the property to look for.</param>
		/// <returns>
		/// Returns a collection containing all the properties of this symbol. Never returns Null.
		/// </returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<IPropertySymbol> GetProperties(this ITypeSymbol type, string? propertyName = null) =>
			GetMembersInternal<IPropertySymbol>(type, propertyName);

		/// <summary>
		/// Get all fields of this <paramref name="type"/>. If <paramref name="fieldName"/> is specified then all fields with this name are returned.
		/// </summary>
		/// <param name="type">The type to act on.</param>
		/// <param name="fieldName">(Optional) Name of the field to look for.</param>
		/// <returns>
		/// Returns a collection containing all the fields of this symbol. Never returns Null.
		/// </returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<IFieldSymbol> GetFields(this ITypeSymbol type, string? fieldName = null) =>
			GetMembersInternal<IFieldSymbol>(type, fieldName);

		private static IEnumerable<TSymbol> GetMembersInternal<TSymbol>(this ITypeSymbol type, string? memberName = null)
		where TSymbol : ISymbol
		{
			ImmutableArray<ISymbol> members = memberName != null
				? type.GetMembers(memberName)
				: type.GetMembers();

			return members.IsDefaultOrEmpty
				? []
				: members.OfType<TSymbol>();
		}

		/// <summary>
		/// Returns true if a type is declared in Acumatica root namespace
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsInAcumaticaRootNamespace(this ITypeSymbol type)
		{
			type.ThrowOnNull();

			var typeRootNamespace = type
				.GetContainingNamespaces()
				.Where(n => !string.IsNullOrEmpty(n.Name))
				.Last();

			return NamespaceNames.AcumaticaRootNamespace.Equals(typeRootNamespace.Name, StringComparison.Ordinal);
		}


		/// <summary>
		/// Gets a simplified name for type if it is a primitive type.
		/// </summary>
		/// <param name="type">The type to act on.</param>
		/// <returns/>
		public static string GetSimplifiedName(this ITypeSymbol type)
		{
			type.ThrowOnNull();

			switch (type.SpecialType)
			{
				case SpecialType.None when type.TypeKind == TypeKind.Array:
				case SpecialType.System_Object:
				case SpecialType.System_Void:
				case SpecialType.System_Boolean:
				case SpecialType.System_Char:
				case SpecialType.System_SByte:
				case SpecialType.System_Byte:
				case SpecialType.System_Int16:
				case SpecialType.System_UInt16:
				case SpecialType.System_Int32:
				case SpecialType.System_UInt32:
				case SpecialType.System_Int64:
				case SpecialType.System_UInt64:
				case SpecialType.System_Decimal:
				case SpecialType.System_Single:
				case SpecialType.System_Double:
				case SpecialType.System_String:
				case SpecialType.System_Array:
				case SpecialType.System_Nullable_T:
					return type.ToString();
				default:
					return type.Name;
			}
		}

		/// <summary>
		/// Check if <paramref name="typeSymbol"/> and all its containing types are <see langword="public"/>.
		/// </summary>
		/// <param name="typeSymbol">The typeSymbol to act on.</param>
		/// <returns>
		/// True if <paramref name="typeSymbol"/> is public with all its containing types, false if not.
		/// </returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsPublicWithAllContainingTypes(this ITypeSymbol typeSymbol) =>
			typeSymbol.CheckIfNull(nameof(typeSymbol))
					  .GetContainingTypesAndThis()
					  .All(type => type.DeclaredAccessibility == Accessibility.Public);
	}
}