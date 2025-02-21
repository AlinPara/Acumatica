﻿#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

using Acuminator.Utilities.Common;
using Acuminator.Utilities.Roslyn.Semantic;
using Acuminator.Utilities.Roslyn.Semantic.Dac;

namespace Acuminator.Vsix.ToolWindows.CodeMap
{
	public class KeyDacFieldsCategoryNodeViewModel : DacFieldCategoryNodeViewModel
	{
		protected override bool AllowNavigation => true;

		public override Icon NodeIcon => Icon.DacKeysCategory;

		public KeyDacFieldsCategoryNodeViewModel(DacNodeViewModel dacViewModel, bool isExpanded) : 
											base(dacViewModel, DacMemberCategory.Keys, isExpanded)
		{		
		}

		public override IEnumerable<DacFieldInfo> GetCategoryDacFields() => 
			DacModel.DeclaredDacFields.Where(field => field.IsKey);

		public override TResult AcceptVisitor<TInput, TResult>(CodeMapTreeVisitor<TInput, TResult> treeVisitor, TInput input) => treeVisitor.VisitNode(this, input);

		public override TResult AcceptVisitor<TResult>(CodeMapTreeVisitor<TResult> treeVisitor) => treeVisitor.VisitNode(this);

		public override void AcceptVisitor(CodeMapTreeVisitor treeVisitor) => treeVisitor.VisitNode(this);
	}
}
