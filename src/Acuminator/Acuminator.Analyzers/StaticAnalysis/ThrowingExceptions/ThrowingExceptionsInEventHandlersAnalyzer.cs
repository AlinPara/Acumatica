﻿#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Acuminator.Analyzers.StaticAnalysis.EventHandlers;
using Acuminator.Analyzers.StaticAnalysis.PXGraph;
using Acuminator.Utilities.Roslyn.Semantic;
using Acuminator.Utilities.Roslyn.Semantic.PXGraph;
using Acuminator.Utilities.Roslyn.Syntax;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Acuminator.Analyzers.StaticAnalysis.ThrowingExceptions
{
	public partial class ThrowingExceptionsInEventHandlersAnalyzer : IEventHandlerAnalyzer, IPXGraphAnalyzer
	{
		public ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
			Descriptors.PX1073_ThrowingExceptionsInRowPersisted,
			Descriptors.PX1073_ThrowingExceptionsInRowPersisted_NonISV,
			Descriptors.PX1074_ThrowingSetupNotEnteredExceptionInEventHandlers);

		bool IEventHandlerAnalyzer.ShouldAnalyze(PXContext pxContext, EventType eventType) => 
			eventType != EventType.None;

		bool IPXGraphAnalyzer.ShouldAnalyze(PXContext pxContext, PXGraphEventSemanticModel graphOrGraphExtension) => 
			graphOrGraphExtension?.IsInSource == true && !graphOrGraphExtension.Symbol.IsStatic;

		/// <summary>
		/// Analyze events outside graphs and graph extensions.
		/// </summary>
		/// <param name="context">The context.</param>
		/// <param name="pxContext">The Acumatica context.</param>
		/// <param name="eventType">Type of the event.</param>
		void IEventHandlerAnalyzer.Analyze(SymbolAnalysisContext context, PXContext pxContext, EventType eventType)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			var methodSymbol = (IMethodSymbol) context.Symbol;

			// For events analyzer filter out all analysis inside graph or graph extension
			// It will be done by the graph analyzer
			if (methodSymbol.ContainingType != null && methodSymbol.ContainingType.IsPXGraphOrExtension(pxContext))	
				return;

			var methodSyntax = methodSymbol.GetSyntax(context.CancellationToken) as CSharpSyntaxNode;

			if (methodSyntax != null)
			{
				var walker = new ThrowInEventsWalker(context, pxContext, eventType);

				methodSyntax.Accept(walker);
			}
		}

		/// <summary>
		/// Analyzes events in graph or graph extension.
		/// </summary>
		/// <param name="context">The context.</param>
		/// <param name="pxContext">The Acumatica context.</param>
		/// <param name="graphOrGraphExtension">The graph or graph extension semantic model with events.</param>
		void IPXGraphAnalyzer.Analyze(SymbolAnalysisContext context, PXContext pxContext, PXGraphEventSemanticModel graphOrExtensionWithEvents)
		{
			context.CancellationToken.ThrowIfCancellationRequested();

			foreach (EventType eventType in Enum.GetValues(typeof(EventType)))
			{
				context.CancellationToken.ThrowIfCancellationRequested();

				if (eventType == EventType.None)
					continue;

				AnalyzeGraphEventsForEventType(eventType, context, pxContext, graphOrExtensionWithEvents);
			}
		}

		private void AnalyzeGraphEventsForEventType(EventType eventType, SymbolAnalysisContext context, PXContext pxContext,
													PXGraphEventSemanticModel graphOrGraphExtensionWithEvents)
		{
			var declaredGraphEventsOfEventType = graphOrGraphExtensionWithEvents
														.GetEventsByEventType(eventType)
														.Where(graphEvent => graphEvent.Symbol.IsDeclaredInType(graphOrGraphExtensionWithEvents.Symbol));
			ThrowInGraphEventsWalker? walker = null;

			foreach (GraphEventInfoBase graphEvent in declaredGraphEventsOfEventType)
			{
				context.CancellationToken.ThrowIfCancellationRequested();
				walker ??= new ThrowInGraphEventsWalker(context, pxContext, eventType, graphOrGraphExtensionWithEvents);

				// Node is not null because analysis runs only for graphs declared in source
				graphEvent.Node!.Accept(walker);
			}
		}
	}
}
