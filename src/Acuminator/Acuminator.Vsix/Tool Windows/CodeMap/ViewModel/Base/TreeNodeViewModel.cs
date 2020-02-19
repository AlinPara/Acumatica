﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Acuminator.Utilities.Common;
using Acuminator.Vsix.Utilities;


namespace Acuminator.Vsix.ToolWindows.CodeMap
{
	public abstract class TreeNodeViewModel : ViewModelBase
	{
		public TreeViewModel Tree { get; }

		public abstract string Name { get; protected set; }

		/// <summary>
		/// True to display node without children in a tree, false if not.
		/// </summary>
		public abstract bool DisplayNodeWithoutChildren { get; }

		/// <summary>
		/// The tooltip displayed for node.
		/// </summary>
		public virtual string Tooltip => null;

		/// <summary>
		/// The icon for a node.
		/// </summary>
		public virtual Icon NodeIcon => Icon.None;

		private SortType _childrenSortType;

		/// <summary>
		/// The sort type of nodes children.
		/// </summary>
		public SortType ChildrenSortType
		{
			get => _childrenSortType;
			protected set
			{
				if (_childrenSortType != value)
				{
					_childrenSortType = value;
					NotifyPropertyChanged();
				}
			}
		}

		private SortDirection _childrenSortDirection = SortDirection.Ascending;

		/// <summary>
		/// The children sort direction.
		/// </summary>
		public SortDirection ChildrenSortDirection
		{
			get => _childrenSortDirection;
			protected set
			{
				if (_childrenSortDirection != value)
				{
					_childrenSortDirection = value;
					NotifyPropertyChanged();
				}
			}
		}

		public ExtendedObservableCollection<TreeNodeViewModel> Children { get; } = new ExtendedObservableCollection<TreeNodeViewModel>();

		private bool _isExpanded;

		public bool IsExpanded
		{
			get => _isExpanded;
			set
			{
				if (_isExpanded != value)
				{
					_isExpanded = value;
					NotifyPropertyChanged();
				}
			}
		}

		public bool IsSelected
		{
			get => ReferenceEquals(this, Tree.SelectedItem);
			set
			{ 
				if (value)
				{
					Tree.SetSelectedWithoutNotification(this);
				}
				else if (ReferenceEquals(this, Tree.SelectedItem))
				{
					Tree.SetSelectedWithoutNotification(null);
				}

				NotifyPropertyChanged();
			}
		}

		protected TreeNodeViewModel(TreeViewModel tree, bool isExpanded = true)
		{
			tree.ThrowOnNull(nameof(tree));

			Tree = tree;
			_isExpanded = isExpanded;
			_childrenSortType = SortType.Declaration;
		}

		public virtual Task NavigateToItemAsync() => Microsoft.VisualStudio.Threading.TplExtensions.CompletedTask;

		public abstract TResult AcceptVisitor<TResult>(CodeMapTreeVisitor<TResult> treeVisitor, CancellationToken cancellationToken);

		public virtual void ExpandOrCollapseAll(bool expand)
		{
			IsExpanded = expand;
			Children.ForEach(childNode => childNode.ExpandOrCollapseAll(expand));
		}

		/// <summary>
		/// This method checks if the node can be sorted with the specified <paramref name="sortType"/> and reordered by sorting of code map nodes. 
		/// Not sortable nodes will always be placed first.
		/// </summary>
		public virtual bool IsSortTypeSupported(SortType sortType) => false;

		//public virtual void AcceptSorter(TreeNodesSorter sorter, SortType sortType, SortDirection sortDirection, bool sortDescendants)
		//{
		//	sorter.ThrowOnNull(nameof(sorter));

		//	ChildrenSortType = sortType;
		//	ChildrenSortDirection = sortDirection;
		//	var sorted = sorter.SortNodes(Children, sortType, sortDirection).ToList(capacity: Children.Count) ?? Enumerable.Empty<TreeNodeViewModel>();

		//	Children.Reset(sorted);

		//	if (sortDescendants && Children.Count > 0)
		//	{
		//		foreach (var childNode in Children)
		//		{
		//			childNode.AcceptSorter(sorter, sortType, sortDirection, sortDescendants);
		//		}
		//	}
		//}
	}
}
