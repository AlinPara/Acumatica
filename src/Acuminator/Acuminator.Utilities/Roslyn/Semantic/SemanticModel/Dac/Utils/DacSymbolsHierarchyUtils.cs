﻿#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using Acuminator.Utilities.Common;
using Acuminator.Utilities.Roslyn.Constants;

using Microsoft.CodeAnalysis;

namespace Acuminator.Utilities.Roslyn.Semantic.Dac
{
	public static class DacSymbolsHierarchyUtils
	{
		public static IEnumerable<ITypeSymbol> GetDacExtensionsWithDac(this ITypeSymbol dacExtension, PXContext pxContext)
		{
			dacExtension.ThrowOnNull();
			pxContext.ThrowOnNull();

			if (!dacExtension.IsDacExtension(pxContext))
			{
				return [];
			}

			var extensionBaseType = dacExtension.BaseType!;
			var typeArguments = extensionBaseType.TypeArguments;
			var dacType = typeArguments.LastOrDefault();

			if (dacType == null || !dacType.IsDAC(pxContext))
			{
				return [];
			}

			var types = new List<ITypeSymbol>(typeArguments.Length + 1) { dacExtension };
			var typeArgumentsExceptDac = typeArguments.Take(typeArguments.Length - 1);

			foreach (var ta in typeArgumentsExceptDac)
			{
				if (!ta.IsDacExtension(pxContext))
				{
					return [];
				}

				types.Add(ta);
			}

			types.Add(dacType);

			return types;
		}

		/// <summary>
		/// Gets the base types of a given <paramref name="dacType"/> that may store DAC properties.<br/>
		/// This includes base types that do not implement IBqlTable interface because they still can declare shared properties.<br/>
		/// The base types are taken up to the <see cref="System.Object"/> or up to the <c>PX.Data.PXBqlTable</c> DAC base type introduced in Acumatica 2024r1.
		/// </summary>
		/// <param name="dacType">The DAC type to act on.</param>
		/// <param name="pxContext">Acumatica context.</param>
		/// <returns>
		/// A collection of <paramref name="dacType"/> base types that may store DAC properties.
		/// </returns>
		/// <remarks>
		/// This helper MUST be called only on DAC types. The behavior on non DAC types is undefined.
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<ITypeSymbol> GetDacBaseTypesThatMayStoreDacProperties(this ITypeSymbol dacType, PXContext pxContext) =>
			GetDacBaseTypesThatMayStoreDacProperties(dacType, pxContext, includeDacType: false);

		/// <summary>
		/// Gets the DAC type <paramref name="dacType"/> with its base types that may store DAC properties.<br/>
		/// This includes base types that do not implement IBqlTable interface because they still can declare shared properties.<br/>
		/// The base types are taken up to the <see cref="System.Object"/> or up to the <c>PX.Data.PXBqlTable</c> DAC base type introduced in Acumatica 2024r1.
		/// </summary>
		/// <param name="dacType">The DAC type to act on.</param>
		/// <param name="pxContext">Acumatica context.</param>
		/// <returns>
		/// A collection containing <paramref name="dacType"/> and its base types that may store DAC properties.
		/// </returns>
		/// <remarks>
		/// This helper MUST be called only on DAC types. The behavior on non DAC types is undefined.
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<ITypeSymbol> GetDacWithBaseTypesThatMayStoreDacProperties(this ITypeSymbol dacType, PXContext pxContext) =>
			GetDacBaseTypesThatMayStoreDacProperties(dacType, pxContext, includeDacType: true);

		private static IEnumerable<ITypeSymbol> GetDacBaseTypesThatMayStoreDacProperties(ITypeSymbol dacType, PXContext pxContext, bool includeDacType)
		{
			dacType.ThrowOnNull();
			var pxBqlTable = pxContext.CheckIfNull().PXBqlTable;

			// Optimization for hot path - most DACs have trivial type hierarchy
			if (dacType.BaseType == null || dacType.BaseType.SpecialType == SpecialType.System_Object ||
				(pxBqlTable != null && dacType.BaseType.Equals(pxBqlTable, SymbolEqualityComparer.Default)))
			{
				return includeDacType ? [dacType] : [];
			}

			var dacHierarchy = dacType.GetBaseTypes();

			// This filter takes all DAC types with a check for the base PXBqlTable type of System.Object type 
			// instead of checking if the type implements IBqlTable interface. This is done to include a useful part of type hierarchy in a scenario 
			// where the base non DAC type which declares some shared fields, for instance, PX.Objects.TX.TaxDetail class.
			if (pxBqlTable != null)
			{
				dacHierarchy = dacHierarchy.TakeWhile(type => !type.Equals(pxBqlTable, SymbolEqualityComparer.Default) &&
							   type.SpecialType != SpecialType.System_Object);
			}
			else
				dacHierarchy = dacHierarchy.TakeWhile(type => type.SpecialType != SpecialType.System_Object);

			if (includeDacType)
				dacHierarchy = dacHierarchy.PrependItem(dacType);

			return dacHierarchy;
		}

		/// <summary>
		/// Gets the DAC extension type with its base types up to first met <c>PX.Data.PXCacheExtension</c>.
		/// </summary>
		/// <param name="extensionType">The DAC extension type to act on.</param>
		/// <returns/>
		public static IEnumerable<ITypeSymbol> GetDacExtensionWithBaseTypes(this ITypeSymbol extensionType) =>
			extensionType.CheckIfNull()
						 .GetBaseTypesAndThis()
						 .TakeWhile(type => !type.IsDacExtensionBaseType());

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsDacExtensionBaseType(this ITypeSymbol type) =>
			type?.Name == TypeNames.PXCacheExtension;

		/// <summary>
		/// Gets the DAC extension with base DAC extensions from DAC extension type.
		/// </summary>
		/// <param name="dacExtension">The DAC extension to act on.</param>
		/// <param name="pxContext">Context.</param>
		/// <param name="sortDirection">The sort direction. The <see cref="SortDirection.Descending"/> order is from the extension to its base extensions/graph.
		/// The <see cref="SortDirection.Ascending"/> order is from the DAC/base extensions to the most derived one.</param>
		/// <param name="includeDac">True to include, false to exclude the DAC type.</param>
		/// <returns/>
		public static IEnumerable<ITypeSymbol> GetDacExtensionWithBaseExtensions(this ITypeSymbol dacExtension, PXContext pxContext,
																				 SortDirection sortDirection, bool includeDac)
		{
			pxContext.ThrowOnNull();

			if (dacExtension == null || !dacExtension.IsDacExtension(pxContext))
				return [];

			var extensionBaseType = dacExtension.GetBaseTypesAndThis()
												.FirstOrDefault(type => type.IsDacExtensionBaseType()) as INamedTypeSymbol;
			if (extensionBaseType == null)
				return [];

			var dacType = extensionBaseType.TypeArguments.LastOrDefault();

			if (dacType == null || !dacType.IsDAC(pxContext))
				return [];

			return sortDirection == SortDirection.Ascending
				? GetExtensionInAscendingOrder(dacType, dacExtension, extensionBaseType, pxContext, includeDac)
				: GetExtensionInDescendingOrder(dacType, dacExtension, extensionBaseType, pxContext, includeDac);
		}

		private static IEnumerable<ITypeSymbol> GetExtensionInAscendingOrder(ITypeSymbol dacType, ITypeSymbol dacExtension,
																			 INamedTypeSymbol extensionBaseType, PXContext pxContext, bool includeDac)
		{
			int dacIndex = extensionBaseType.TypeArguments.Length - 1;
			var extensions = new List<ITypeSymbol>(capacity: extensionBaseType.TypeArguments.Length);

			if (includeDac)
			{
				var baseDacTypes = dacType.GetDacWithBaseTypesThatMayStoreDacProperties(pxContext)
										  .Reverse();
				extensions.AddRange(baseDacTypes);
			}

			for (int i = dacIndex - 1; i >= 0; i--)
			{
				var baseExtension = extensionBaseType.TypeArguments[i];

				if (!baseExtension.IsDacExtension(pxContext))
					return [];

				extensions.Add(baseExtension);      //According to Platform team we shouldn't consider case when the extensions chaining mixes with .Net inheritance
			}

			extensions.AddRange(dacExtension.GetDacExtensionWithBaseTypes().Reverse());
			return extensions.Distinct<ITypeSymbol>(SymbolEqualityComparer.Default);
		}

		private static IEnumerable<ITypeSymbol> GetExtensionInDescendingOrder(ITypeSymbol dacType, ITypeSymbol dacExtension,
																			  INamedTypeSymbol extensionBaseType, PXContext pxContext, bool includeDac)
		{
			int dacIndex = extensionBaseType.TypeArguments.Length - 1;
			var extensions = new List<ITypeSymbol>(capacity: extensionBaseType.TypeArguments.Length);
			extensions.AddRange(dacExtension.GetDacExtensionWithBaseTypes());

			for (int i = 0; i <= dacIndex - 1; i++)
			{
				var baseExtension = extensionBaseType.TypeArguments[i];

				if (!baseExtension.IsDacExtension(pxContext))
					return [];

				extensions.Add(baseExtension);      //According to Platform team we shouldn't consider case when the extensions chaining mixes with .Net inheritance
			}

			if (includeDac)
			{
				var baseDacTypes = dacType.GetDacWithBaseTypesThatMayStoreDacProperties(pxContext);
				extensions.AddRange(baseDacTypes);
			}

			return extensions.Distinct<ITypeSymbol>(SymbolEqualityComparer.Default);
		}
	}
}
