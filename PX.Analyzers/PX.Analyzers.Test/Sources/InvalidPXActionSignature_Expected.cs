﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PX.Common;
using PX.Data;
using System.Collections;

namespace PX.Objects.SO
{
	public class SOEntry : PXGraph<SOEntry>
	{
		public PXSelect<SOOrder> Documents;

		public PXAction<SOOrder> Release;

		public IEnumerable release(PXAdapter adapter)
		{
		    return adapter.Get();
		}
	}

	public class SOOrder : IBqlTable
	{
		public abstract class orderType { }
		[PXDBString(IsKey = true, InputMask = "")]
		[PXDefault]
		public string OrderType { get; set; }

		public abstract class orderNbr { }
		[PXDBString(IsKey = true, InputMask = "")]
		[PXDefault]
		public string OrderNbr { get; set; }
	}
}
