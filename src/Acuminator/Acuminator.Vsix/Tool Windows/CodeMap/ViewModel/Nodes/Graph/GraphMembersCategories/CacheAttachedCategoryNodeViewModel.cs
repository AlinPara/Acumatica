﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Acuminator.Utilities.Common;
using Acuminator.Utilities.Roslyn.Semantic;
using Acuminator.Utilities.Roslyn.Semantic.PXGraph;
using Acuminator.Vsix.Utilities;



namespace Acuminator.Vsix.ToolWindows.CodeMap
{
	public class CacheAttachedCategoryNodeViewModel : GraphEventCategoryNodeViewModel
	{
		public CacheAttachedCategoryNodeViewModel(GraphNodeViewModel graphViewModel, bool isExpanded) :
									base(graphViewModel, GraphMemberType.CacheAttached, isExpanded)
		{
		}

		protected override IEnumerable<GraphNodeSymbolItem> GetCategoryGraphNodeSymbols() => GraphSemanticModel.CacheAttachedEvents;

		public override IEnumerable<TreeNodeViewModel> GetEventsViewModelsForDAC(DacEventsGroupingNodeViewModel dacVM,
																				 IEnumerable<GraphEventInfoBase> graphEventsForDAC,
																				 bool areChildrenExpanded)
		{
			return graphEventsForDAC.OfType<GraphFieldEventInfo>()
									.Select(eventInfo => CreateNewEventVM(dacVM, eventInfo, areChildrenExpanded))
									.Where(graphMemberVM => graphMemberVM != null && !graphMemberVM.Name.IsNullOrEmpty())
									.OrderBy(graphMemberVM => graphMemberVM.Name);
		}

		public override GraphMemberNodeViewModel CreateNewEventVM<TEventNodeParent>(TEventNodeParent eventNodeParent, GraphEventInfoBase eventInfo,
																					bool isExpanded)
		{
			return eventNodeParent is DacEventsGroupingNodeViewModel dacGroupVM && eventInfo is GraphFieldEventInfo fieldEventInfo
				? new CacheAttachedNodeViewModel(dacGroupVM, fieldEventInfo, isExpanded)
				: base.CreateNewEventVM(eventNodeParent, eventInfo, isExpanded);
		}	
	}
}
