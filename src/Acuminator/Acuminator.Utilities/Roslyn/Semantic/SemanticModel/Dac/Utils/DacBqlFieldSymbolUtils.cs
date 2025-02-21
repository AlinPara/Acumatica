﻿#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Acuminator.Utilities.Common;
using Acuminator.Utilities.Roslyn.Constants;
using Acuminator.Utilities.Roslyn.Syntax;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;


namespace Acuminator.Utilities.Roslyn.Semantic.Dac
{
	public static partial class DacPropertyAndFieldSymbolUtils
	{
		/// <summary>
		/// Get corresponding DAC BQL field for a DAC field property <paramref name="property"/>.
		/// </summary>
		/// <param name="property">The property to act on. Must be a dac property.</param>
		/// <param name="pxContext">Acumatica Context.</param>
		/// <param name="checkContainingTypeIsDac">True for extra safety check that containing type is DAC or DAC extension.</param>
		/// <returns>
		/// The corresponding BQL field or null.
		/// </returns>
		public static INamedTypeSymbol? GetCorrespondingBqlField(this IPropertySymbol property, PXContext pxContext, bool checkContainingTypeIsDac)
		{
			property.ThrowOnNull();
			pxContext.ThrowOnNull();

			var containingDacOrDacExt = property.ContainingType;

			if (containingDacOrDacExt == null ||
				(checkContainingTypeIsDac && !containingDacOrDacExt.IsDacOrExtension(pxContext)))
			{
				return null;
			}

			var mappedBqlField = GetBqlFieldFromTypeByName(containingDacOrDacExt, property.Name);

			if (mappedBqlField != null)
				return mappedBqlField;

			var currentType = containingDacOrDacExt.BaseType;

			while (currentType != null && currentType.IsDAC(pxContext))
			{
				mappedBqlField = GetBqlFieldFromTypeByName(currentType, property.Name);

				if (mappedBqlField != null)
					return mappedBqlField;

				currentType = currentType.BaseType;
			}

			return null;
		}

		private static INamedTypeSymbol? GetBqlFieldFromTypeByName(INamedTypeSymbol type, string caseInsensitiveName)
		{
			var members = type.GetMembers();

			if (members.IsDefaultOrEmpty)
				return null;

			return members.OfType<INamedTypeSymbol>()
						  .FirstOrDefault(bqlField => caseInsensitiveName.Equals(bqlField.Name, StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Gets the DAC BQL field data type from BQL field symbol.
		/// </summary>
		/// <param name="bqlField">The BQL field symbol.</param>
		/// <returns>
		/// The BQL field data type (like <see cref="string"/>) from BQL field symbol.
		/// </returns>
		public static ITypeSymbol? GetBqlFieldDataTypeFromBqlFieldSymbol(this ITypeSymbol bqlField)
		{
			bqlField.ThrowOnNull();

			if (bqlField.BaseType == null || bqlField.BaseType.Name != TypeNames.BqlField.Field)
				return null;

			var bqlFieldBqlDataType = bqlField.BaseType.ContainingType;		// this symbol represents types like BqlString, BqlInt, etc.

			if (bqlFieldBqlDataType?.Name != TypeNames.BqlField.BqlType || !bqlFieldBqlDataType.IsGenericType)
				return null;

			var bqlFieldTypeGenericTypeArgs = bqlFieldBqlDataType.TypeArguments;

			if (bqlFieldTypeGenericTypeArgs.Length != 2)
				return null;

			var bqlFieldDataType = bqlFieldTypeGenericTypeArgs[1];
			return bqlFieldDataType;
		}

		/// <summary>
		/// Get the DAC BQL fields symbols and syntax nodes from the DAC.
		/// </summary>
		/// <param name="dac">The DAC to act on.</param>
		/// <param name="pxContext">Context.</param>
		/// <param name="includeFromInheritanceChain">(Optional) True to include, false to exclude the DAC fields from the inheritance chain.</param>
		/// <param name="cancellation">(Optional) Cancellation token.</param>
		/// <returns>
		/// The DAC fields from DAC.
		/// </returns>
		public static OverridableItemsCollection<DacBqlFieldInfo> GetDacBqlFieldsFromDac(this ITypeSymbol dac, PXContext pxContext, 
																						 bool includeFromInheritanceChain = true,
																						 CancellationToken cancellation = default)
		{
			dac.ThrowOnNull();
			pxContext.ThrowOnNull();

			if (!dac.IsDAC(pxContext))
				return new OverridableItemsCollection<DacBqlFieldInfo>();

			int estimatedCapacity = dac.GetTypeMembers().Length;
			var dacFieldsByName = new OverridableItemsCollection<DacBqlFieldInfo>(estimatedCapacity);
			var dacFields = GetRawDacBqlFieldsFromDacImpl(dac, pxContext, includeFromInheritanceChain, cancellation);

			dacFieldsByName.AddRangeWithDeclarationOrder(dacFields, startingOrder: 0, 
												(dacField, order) => DacBqlFieldInfo.CreateUnsafe(pxContext, dacField.Node, dacField.Symbol, order));
			return dacFieldsByName;
		}

		/// <summary>
		/// Get the DAC BQL field symbols and syntax nodes from the DAC extension.
		/// </summary>
		/// <param name="dacExtension">The DAC extension to act on.</param>
		/// <param name="pxContext">Context.</param>
		/// <param name="cancellation">Cancellation token.</param>
		/// <returns>
		/// The DAC fields from DAC extension and base DAC.
		/// </returns>
		public static OverridableItemsCollection<DacBqlFieldInfo> GetDacBqlFieldsFromDacExtensionAndBaseDac(this ITypeSymbol dacExtension, PXContext pxContext,
																											CancellationToken cancellation = default)
		{
			dacExtension.ThrowOnNull();
			pxContext.ThrowOnNull();

			return GetPropertiesOrFieldsInfoFromDacExtension<DacBqlFieldInfo>(dacExtension, pxContext, AddFieldsFromDac, AddFieldsFromDacExtension);


			int AddFieldsFromDac(OverridableItemsCollection<DacBqlFieldInfo> fieldsCollection, ITypeSymbol dac, int startingOrder)
			{
				var rawDacFields = dac.GetRawDacBqlFieldsFromDacImpl(pxContext, includeFromInheritanceChain: true, cancellation);
				return fieldsCollection.AddRangeWithDeclarationOrder(rawDacFields, startingOrder, 
															(dacField, order) => DacBqlFieldInfo.CreateUnsafe(pxContext, dacField.Node, dacField.Symbol, order));
			}

			int AddFieldsFromDacExtension(OverridableItemsCollection<DacBqlFieldInfo> fieldsCollection, ITypeSymbol dacExt, int startingOrder)
			{
				var rawDacExtensionFields = dacExt.GetRawDacBqlFieldsFromDacOrDacExtension(pxContext, cancellation);
				return fieldsCollection.AddRangeWithDeclarationOrder(rawDacExtensionFields, startingOrder,
															(dacField, order) => DacBqlFieldInfo.CreateUnsafe(pxContext, dacField.Node, dacField.Symbol, order));
			}
		}

		private static IEnumerable<(ClassDeclarationSyntax? Node, INamedTypeSymbol Symbol)> GetRawDacBqlFieldsFromDacImpl(this ITypeSymbol dac,
																								PXContext pxContext, bool includeFromInheritanceChain,
																								CancellationToken cancellation)
		{
			if (includeFromInheritanceChain)
			{
				return dac.GetDacWithBaseTypesThatMayStoreDacProperties(pxContext)
						  .Reverse()
						  .SelectMany(baseGraph => GetRawDacBqlFieldsFromDacOrDacExtension(baseGraph, pxContext, cancellation));
			}
			else
			{
				return GetRawDacBqlFieldsFromDacOrDacExtension(dac, pxContext, cancellation);
			}
		}

		private static IEnumerable<(ClassDeclarationSyntax? Node, INamedTypeSymbol Symbol)> GetRawDacBqlFieldsFromDacOrDacExtension(
																								this ITypeSymbol dacOrDacExtension,
																								PXContext pxContext, CancellationToken cancellation)
		{
			IEnumerable<INamedTypeSymbol> dacBqlFields = dacOrDacExtension.GetTypeMembers()
																		  .Where(type => type.IsDacBqlField(pxContext)); 
			foreach (INamedTypeSymbol bqlField in dacBqlFields)
			{
				cancellation.ThrowIfCancellationRequested();
				var bqlFieldNode = bqlField.GetSyntax(cancellation) as ClassDeclarationSyntax;

				yield return (bqlFieldNode, bqlField);
			}
		}
	}
}
