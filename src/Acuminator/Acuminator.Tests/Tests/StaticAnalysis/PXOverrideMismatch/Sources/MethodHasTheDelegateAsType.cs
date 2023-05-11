﻿using System;

namespace Acuminator.Tests.Sources
{
	public class SuperBaseClass : PX.Data.PXGraphExtension<MyGraph>
	{
		public virtual object TestMethod(int x, bool drilldown, double y)
		{
			return new object();
		}
	}

	public class BaseClass : PX.Data.PXGraphExtension<SuperBaseClass, MyGraph>
	{
		[PX.Data.PXOverride]
		public virtual object TestMethod(int x, bool drilldown, double y)
		{
			return new object();
		}
	}

	public class ExtClass : PX.Data.PXGraphExtension<BaseClass, MyGraph>
	{
		public delegate object MyDelegate(int x, bool drilldown, double y);

		[PX.Data.PXOverride]
		public object TestMethod(int x, bool drilldown, double y, MyDelegate del)
		{
			return new object();
		}
	}

	public class MyGraph : PX.Data.PXGraph
	{

	}
}

namespace PX.Data
{
	public class PXOverrideAttribute : Attribute
	{
	}

	public abstract class PXGraph
	{
	}

	public abstract class PXGraphExtension
	{
	}

	public abstract class PXGraphExtension<Graph> : PXGraphExtension
		where Graph : PXGraph
	{
		internal Graph _Base;
	}

	public abstract class PXGraphExtension<Extension1, Graph> : PXGraphExtension
		where Graph : PXGraph
		where Extension1 : PXGraphExtension<Graph>
	{
		internal Extension1 MyExt;

		internal Graph MyGraph;
	}
}