﻿#nullable enable

using System;
using System.Collections.Generic;

namespace Acuminator.Vsix.ToolWindows.CodeMap
{
	/// <summary>
	/// Base class for code map tree visitor which produces a result.
	/// </summary>
	public abstract partial class CodeMapTreeVisitor<TResult>
	{
		#region Roots
		public virtual TResult VisitNode(GraphNodeViewModel graph) => DefaultVisit(graph);
		#endregion

		#region Categories
		public virtual TResult VisitNode(ActionCategoryNodeViewModel actionCategory) => DefaultVisit(actionCategory);

		public virtual TResult VisitNode(ViewCategoryNodeViewModel viewCategory) => DefaultVisit(viewCategory);

		public virtual TResult VisitNode(CacheAttachedCategoryNodeViewModel cacheAttachedCategory) => DefaultVisit(cacheAttachedCategory);

		public virtual TResult VisitNode(RowEventCategoryNodeViewModel rowEventCategory) => DefaultVisit(rowEventCategory);

		public virtual TResult VisitNode(FieldEventCategoryNodeViewModel rowEventCategory) => DefaultVisit(rowEventCategory);

		public virtual TResult VisitNode(PXOverridesCategoryNodeViewModel pxOverridesCategory) => DefaultVisit(pxOverridesCategory);

		public virtual TResult VisitNode(GraphInitializationAndActivationCategoryNodeViewModel graphInitializationAndActivationCategory) => 
			DefaultVisit(graphInitializationAndActivationCategory);

		public virtual TResult VisitNode(GraphBaseMemberOverridesCategoryNodeViewModel graphBaseMemberOverridesCategory) =>
			DefaultVisit(graphBaseMemberOverridesCategory);
		#endregion

		#region DAC Grouping
		public virtual TResult VisitNode(DacGroupingNodeForRowEventViewModel dacGroupingNode) => DefaultVisit(dacGroupingNode);

		public virtual TResult VisitNode(DacGroupingNodeForCacheAttachedEventViewModel dacGroupingNode) => DefaultVisit(dacGroupingNode);

		public virtual TResult VisitNode(DacGroupingNodeForFieldEventViewModel dacGroupingNode) => DefaultVisit(dacGroupingNode);

		public virtual TResult VisitNode(DacFieldGroupingNodeForFieldEventViewModel dacFieldGroupingNode) => DefaultVisit(dacFieldGroupingNode);
		#endregion

		#region Leaf Nodes
		public virtual TResult VisitNode(PXOverrideNodeViewModel pxOverrideNode) => DefaultVisit(pxOverrideNode);

		public virtual TResult VisitNode(ActionNodeViewModel actionNode) => DefaultVisit(actionNode);

		public virtual TResult VisitNode(ViewNodeViewModel viewNode) => DefaultVisit(viewNode);

		public virtual TResult VisitNode(CacheAttachedNodeViewModel cacheAttachedNode) => DefaultVisit(cacheAttachedNode);

		public virtual TResult VisitNode(RowEventNodeViewModel rowEventNode) => DefaultVisit(rowEventNode);

		public virtual TResult VisitNode(FieldEventNodeViewModel fieldEventNode) => DefaultVisit(fieldEventNode);

		public virtual TResult VisitNode(GraphMemberInfoNodeViewModel graphMemberInfo) => DefaultVisit(graphMemberInfo);

		public virtual TResult VisitNode(IsActiveGraphMethodNodeViewModel isActiveGraphMethodNode) => 
			DefaultVisit(isActiveGraphMethodNode);

		public virtual TResult VisitNode(IsActiveForGraphMethodNodeViewModel isActiveForGraphMethodNode) =>
			DefaultVisit(isActiveForGraphMethodNode);

		public virtual TResult VisitNode(GraphConfigureMethodNodeViewModel configureMethodNode) =>
			DefaultVisit(configureMethodNode);

		public virtual TResult VisitNode(GraphInitializeMethodNodeViewModel initializedMethodNode) =>
			DefaultVisit(initializedMethodNode);

		public virtual TResult VisitNode(GraphInstanceConstructorNodeViewModel graphInstanceConstructorNode) =>
			DefaultVisit(graphInstanceConstructorNode);

		public virtual TResult VisitNode(GraphStaticConstructorNodeViewModel graphInstanceConstructorNode) =>
			DefaultVisit(graphInstanceConstructorNode);

		public virtual TResult VisitNode(GraphBaseMembeOverrideNodeViewModel graphBaseMembeOverrideNode) =>
			DefaultVisit(graphBaseMembeOverrideNode);
		#endregion

		#region Attribute Nodes
		public virtual TResult VisitNode(GraphAttributesGroupNodeViewModel attributeGroupNode) => DefaultVisit(attributeGroupNode);

		public virtual TResult VisitNode(CacheAttachedAttributeNodeViewModel attributeNode) => DefaultVisit(attributeNode);

		public virtual TResult VisitNode(GraphAttributeNodeViewModel attributeNode) => DefaultVisit(attributeNode);
		#endregion
	}
}
