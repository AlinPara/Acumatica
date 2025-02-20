﻿using System;

using PX.Data;

namespace PX.Analyzers.Test.Sources
{
	[PXHidden]
	public class DerivedDac : BaseDac
	{
		#region Shipment Nbr
		[PXUIField(DisplayName = "Shipment Nbr.")]
		public override int? ShipmentNbr { get; set; }
		#endregion
	}

	[PXHidden]
	public class BaseDac : IBqlTable
	{
		#region Status
		[PXString]
		[PXUIField(DisplayName = "Status")]
		public string Status { get; set; }

		public abstract class status : PX.Data.BQL.BqlString.Field<status> { }
		#endregion

		#region tstamp
		public abstract class Tstamp : PX.Data.BQL.BqlByteArray.Field<Tstamp> { }

		[PXDBTimestamp(RecordComesFirst = true)]
		public virtual byte[] tstamp { get; set; }
		#endregion

		public abstract class Tstamp2 : PX.Data.BQL.BqlByteArray.Field<Tstamp2> { }

		#region Shipment Nbr - BQL type is taken from the property type
		public abstract class shipmentNbr : IBqlField { }

		[PXInt]
		public virtual int? ShipmentNbr { get; set; }
		#endregion

		#region tstamp3 - BQL type is taken from the property type
		[PXDBTimestamp(RecordComesFirst = true)]
		public virtual byte[] tstamp3 { get; set; }

		public abstract class Tstamp3 : IBqlField { }
		#endregion
	}
}