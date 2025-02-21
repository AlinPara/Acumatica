﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PX.Data;

namespace Acuminator.Tests.Tests.StaticAnalysis.NonPublicGraphsDacsAndExtensions.Sources
{
	public class SOOrderExt1 : PXGraphExtension<SOOrderEntry>
	{
		public virtual void _(Events.RowUpdating<SOOrder> e)
		{

		}
	}

	public class SOOrderExt2 : PXGraphExtension<SOOrderEntry>
	{
		public virtual void _(Events.RowUpdated<SOOrder> e)
		{

		}
	}

	public static class PurchaseGlobalState
	{
		public class PurchaseEngine
		{
			public sealed class SOOrderEntryExtPurchase : PXGraphExtension<SOOrderEntry>
			{
				public virtual void _(Events.RowSelected<SOOrder> e)
				{

				}
			}

			public sealed class SOOrderEntryExtPurchase2 : PXGraphExtension<SOOrderEntry>
			{
				public virtual void _(Events.RowSelected<SOOrder> e)
				{

				}
			}
		}
	}


	public class SOOrderEntry : PXGraph<SOOrderEntry>
	{

	}

	[PXHidden]
	public class SOOrder : IBqlTable
	{

	}
}
