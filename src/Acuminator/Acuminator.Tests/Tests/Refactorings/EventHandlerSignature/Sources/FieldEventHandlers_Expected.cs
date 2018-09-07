﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PX.Data;

namespace PX.Objects
{
	public class SOInvoiceEntry : PXGraph<SOInvoiceEntry, SOInvoice>
	{
		protected virtual void _(Events.FieldSelecting<SOInvoice.refNbr> e)
		{
			
		}

		protected virtual void _(Events.FieldDefaulting<SOInvoice.refNbr> e)
		{

		}

		protected virtual void _(Events.FieldVerifying<SOInvoice.refNbr> e)
		{

		}

		protected virtual void _(Events.FieldUpdating<SOInvoice.refNbr> e)
		{

		}

		protected virtual void _(Events.FieldUpdated<SOInvoice.refNbr> e)
		{

		}

		protected virtual void _(Events.CommandPreparing<SOInvoice.refNbr> e)
		{

		}
	}

	public class SOInvoice : IBqlTable
	{
		#region RefNbr
		[PXDBString(8, IsKey = true, InputMask = "")]
		public string RefNbr { get; set; }
		public abstract class refNbr : IBqlField { }
		#endregion	
	}
}