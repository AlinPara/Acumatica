﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Threading;
using Acuminator.Utilities.Common;
using Acuminator.Vsix.Utilities;


using ThreadHelper = Microsoft.VisualStudio.Shell.ThreadHelper;


namespace Acuminator.Vsix.ToolWindows.CodeMap
{
	public class CodeMapWindowViewModel : ToolWindowViewModelBase
	{
		private CancellationTokenSource _cancellationTokenSource;

		private DocumentModel _documentModel;
		private Workspace _workspace;

		public Document Document => _documentModel?.Document;

		private TreeViewModel _tree;

		public TreeViewModel Tree
		{
			get => _tree;
			private set
			{
				if (!ReferenceEquals(_tree, value))
				{
					_tree = value;
					NotifyPropertyChanged();
				}
			}
		}

		public CancellationToken? CancellationToken => _cancellationTokenSource?.Token;

		private bool _isCalculating;

		public bool  IsCalculating
		{
			get => _isCalculating;
			private set
			{
				if (_isCalculating != value)
				{
					_isCalculating = value;
					NotifyPropertyChanged();
				}
			}
		}

		private CodeMapWindowViewModel(IWpfTextView wpfTextView, Document document)
		{
			_documentModel = new DocumentModel(wpfTextView, document);
			Tree = new TreeViewModel(this);

			_workspace = _documentModel.Document.Project.Solution.Workspace;
			_workspace.WorkspaceChanged += OnWorkspaceChanged;
			SubscribeOnWindowChangeEvent();
		}

		public static CodeMapWindowViewModel InitCodeMap(IWpfTextView wpfTextView, Document document)
		{
			if (wpfTextView == null || document == null)
				return null;

			var codeMapViewModel = new CodeMapWindowViewModel(wpfTextView, document);
			codeMapViewModel.BuildCodeMapAsync().Forget();
			return codeMapViewModel;
		}

		public void CancelCodeMapBuilding()
		{
			_cancellationTokenSource?.Cancel();
		}

		public override void FreeResources()
		{
			base.FreeResources();

			_cancellationTokenSource?.Dispose();

			if (_workspace != null)
			{
				_workspace.WorkspaceChanged -= OnWorkspaceChanged;
			}

			UnsubscribeFromWindowChangeEvent();
		}

		private void SubscribeOnWindowChangeEvent()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			EnvDTE.DTE dte = AcuminatorVSPackage.Instance.GetService<EnvDTE.DTE>();

			if (dte == null)
				return;

			dte.Events.WindowEvents.WindowActivated += WindowEvents_WindowActivated;
		}

		private void UnsubscribeFromWindowChangeEvent()
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			EnvDTE.DTE dte = AcuminatorVSPackage.Instance.GetService<EnvDTE.DTE>();

			if (dte == null)
				return;

			dte.Events.WindowEvents.WindowActivated -= WindowEvents_WindowActivated;
		}

		private void WindowEvents_WindowActivated(EnvDTE.Window gotFocus, EnvDTE.Window lostFocus)
		{
			if (!ThreadHelper.CheckAccess() || Equals(gotFocus, lostFocus) || gotFocus?.Document == null)
			{
				return;
			}

			#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
			if (gotFocus.Document.Language != LegacyLanguageNames.CSharp || gotFocus.Document.Path == lostFocus?.Document?.Path)
			{
				return;
			}
			#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread

			var currentWorkspace = AcuminatorVSPackage.Instance.GetVSWorkspace();

			if (currentWorkspace != null)
			{
				HandleActiveDocumentChangedOrClosed(currentWorkspace);
			}		
		}

		private async void OnWorkspaceChanged(object sender, WorkspaceChangeEventArgs e)
		{
			if (e == null || !(sender is Workspace newWorkspace))
				return;

			if (!ThreadHelper.CheckAccess())
			{
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken ?? default);
			}

			bool isDocumentTabClosed = _documentModel.WpfTextView?.IsClosed ?? true;

			if (Document == null || isDocumentTabClosed || e.IsActiveDocumentCleared(Document) || e.IsActiveDocumentChanged(Document))
			{
				HandleActiveDocumentChangedOrClosed(newWorkspace);
			}
			else if (e.IsDocumentTextChanged(Document))
			{
				await HandleDocumentTextChangesAsync(newWorkspace, e).ConfigureAwait(false);
			}		
		}

		private void HandleActiveDocumentChangedOrClosed(Workspace newWorkspace)
		{
			ClearCodeMap();

			_workspace = newWorkspace;
			IWpfTextView newWpfTextView = AcuminatorVSPackage.Instance.GetWpfTextView();
			Document newDocument = newWpfTextView?.TextSnapshot.GetOpenDocumentInCurrentContextWithChanges();

			if (newDocument == null)
				return;

			_documentModel = new DocumentModel(newWpfTextView, newDocument);
			BuildCodeMapAsync().Forget();
		}

		private async Task HandleDocumentTextChangesAsync(Workspace newWorkspace, WorkspaceChangeEventArgs e)
		{
			ClearCodeMap();

			_workspace = newWorkspace;
			Document changedDocument = e.NewSolution.GetDocument(e.DocumentId);

			if (changedDocument == null)
				return;

			var root = await changedDocument.GetSyntaxRootAsync().ConfigureAwait(false);

			if (root == null || root.ContainsDiagnostics)
				return;

			_documentModel = new DocumentModel(_documentModel.WpfTextView, changedDocument);
			BuildCodeMapAsync().Forget();
		}

		private void ClearCodeMap()
		{
			_cancellationTokenSource?.Cancel();
			Tree?.RootItems.Clear();
			Tree = null;
		}

		private async Task BuildCodeMapAsync()
		{
			try
			{
				using (_cancellationTokenSource = new CancellationTokenSource())
				{
					CancellationToken cancellationToken = _cancellationTokenSource.Token;

					if (!ThreadHelper.CheckAccess())
					{
						await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
					}
				
					IsCalculating = true;
					await TaskScheduler.Default;

					await _documentModel.LoadCodeFileDataAsync(cancellationToken)
										.ConfigureAwait(false);

					TreeViewModel newTreeVM = BuildCodeMapTreeView(cancellationToken);

					if (newTreeVM == null)
						return;

					await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

					Tree = newTreeVM;
					IsCalculating = false;
				}
			}
			catch (OperationCanceledException e)
			{

			}
			finally
			{
				_cancellationTokenSource = null;
			}
		}

		private TreeViewModel BuildCodeMapTreeView(CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested || !_documentModel.IsCodeFileDataLoaded)
				return null;

			TreeViewModel tree = new TreeViewModel(this);
			var rootItems = _documentModel.GraphModels.Select(graph => GraphNodeViewModel.Create(graph, tree))
													  .Where(graphVM => graphVM != null);
			tree.RootItems.AddRange(rootItems);
			cancellationToken.ThrowIfCancellationRequested();
			return tree;
		}
	}
}
