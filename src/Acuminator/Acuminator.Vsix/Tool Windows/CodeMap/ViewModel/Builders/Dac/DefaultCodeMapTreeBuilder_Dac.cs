﻿#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Acuminator.Utilities.Common;
using Acuminator.Utilities.Roslyn.Semantic;
using Acuminator.Utilities.Roslyn.Semantic.Dac;
using Acuminator.Utilities.Roslyn.Semantic.SharedInfo;

namespace Acuminator.Vsix.ToolWindows.CodeMap
{
	public partial class DefaultCodeMapTreeBuilder : TreeBuilderBase
	{
		protected virtual DacNodeViewModel CreateDacNode(DacSemanticModel dacSemanticModel, TreeViewModel tree) =>
			new DacNodeViewModel(dacSemanticModel, tree, ExpandCreatedNodes);

		public override IEnumerable<TreeNodeViewModel>? VisitNode(DacNodeViewModel dac)
		{
			var dacAttributesGroup = GetDacAttributesGroupNode(dac);

			if (dacAttributesGroup != null)
				yield return dacAttributesGroup;

			foreach (DacMemberCategory dacMemberCategory in GetDacMemberCategoriesInOrder())
			{
				Cancellation.ThrowIfCancellationRequested();
				var dacCategory = CreateCategory(dac, dacMemberCategory);

				if (dacCategory != null)
				{
					yield return dacCategory;
				}
			}
		}

		protected virtual DacAttributesGroupNodeViewModel GetDacAttributesGroupNode(DacNodeViewModel dac) =>
			new DacAttributesGroupNodeViewModel(dac.DacModel, dac, ExpandCreatedNodes);

		protected virtual IEnumerable<DacMemberCategory> GetDacMemberCategoriesInOrder()
		{
			yield return DacMemberCategory.InitializationAndActivation;
			yield return DacMemberCategory.Keys;
			yield return DacMemberCategory.Property;
			yield return DacMemberCategory.FieldsWithoutProperty;
		}

		protected virtual DacMemberCategoryNodeViewModel? CreateCategory(DacNodeViewModel dac, DacMemberCategory dacMemberCategory) =>
			dacMemberCategory switch
			{
				DacMemberCategory.InitializationAndActivation => new DacInitializationAndActivationCategoryNodeViewModel(dac, ExpandCreatedNodes),
				DacMemberCategory.Keys 						  => new KeyDacFieldsCategoryNodeViewModel(dac, ExpandCreatedNodes),
				DacMemberCategory.Property 					  => new AllDacFieldsDacCategoryNodeViewModel(dac, ExpandCreatedNodes),
				_ 											  => null,
			};

		public override IEnumerable<TreeNodeViewModel> VisitNode(DacAttributesGroupNodeViewModel attributeGroupNode) =>
			attributeGroupNode.AttributeInfos()
							  .Select(attrInfo => new DacAttributeNodeViewModel(attributeGroupNode, attrInfo, ExpandCreatedNodes));

		public override IEnumerable<TreeNodeViewModel>? VisitNode(DacInitializationAndActivationCategoryNodeViewModel dacInitializationAndActivationCategory)
		{
			Cancellation.ThrowIfCancellationRequested();

			if (dacInitializationAndActivationCategory?.DacModel.IsActiveMethodInfo != null)
			{
				var isActiveNode = new IsActiveDacMethodNodeViewModel(dacInitializationAndActivationCategory,
																	  dacInitializationAndActivationCategory.DacModel.IsActiveMethodInfo, 
																	  ExpandCreatedNodes);
				return [isActiveNode];
			}
			else
				return [];
		}

		public override IEnumerable<TreeNodeViewModel>? VisitNode(KeyDacFieldsCategoryNodeViewModel dacKeyFieldsCategory) =>
			CreateDacFieldsCategoryChildren(dacKeyFieldsCategory);

		public override IEnumerable<TreeNodeViewModel>? VisitNode(AllDacFieldsDacCategoryNodeViewModel allDacFieldsCategory) =>
			CreateDacFieldsCategoryChildren(allDacFieldsCategory);

		protected virtual IEnumerable<TreeNodeViewModel> CreateDacFieldsCategoryChildren(DacFieldCategoryNodeViewModel dacFieldCategory)
		{
			var categorySymbols = dacFieldCategory.CheckIfNull().GetCategoryDacFields();

			if (categorySymbols == null)
				yield break;

			foreach (DacFieldInfo fieldInfo in categorySymbols)
			{
				Cancellation.ThrowIfCancellationRequested();
				TreeNodeViewModel childNode = new DacFieldGroupingNodeViewModel(dacFieldCategory, parent: dacFieldCategory, 
																				fieldInfo, ExpandCreatedNodes);
				if (childNode != null)
					yield return childNode;
			}
		}

		public override IEnumerable<TreeNodeViewModel>? VisitNode(DacFieldGroupingNodeViewModel dacField)
		{
			if (dacField.FieldInfo.PropertyInfo != null)
			{
				yield return new DacFieldPropertyNodeViewModel(dacField.MemberCategory, parent: dacField, 
															   dacField.FieldInfo.PropertyInfo, ExpandCreatedNodes);
			}

			if (dacField.FieldInfo.BqlFieldInfo != null)
			{
				yield return new DacBqlFieldNodeViewModel(dacField.MemberCategory, parent: dacField, 
														   dacField.FieldInfo.BqlFieldInfo, ExpandCreatedNodes);
			}
		}

		public override IEnumerable<TreeNodeViewModel> VisitNode(DacFieldPropertyNodeViewModel dacFieldProperty)
		{
			var attributes = dacFieldProperty.CheckIfNull().PropertyInfo.Attributes;
			return !attributes.IsDefaultOrEmpty
				? attributes.Select(attrInfo => new DacFieldAttributeNodeViewModel(dacFieldProperty, attrInfo, ExpandCreatedNodes))
				: [];
		}
	}
}