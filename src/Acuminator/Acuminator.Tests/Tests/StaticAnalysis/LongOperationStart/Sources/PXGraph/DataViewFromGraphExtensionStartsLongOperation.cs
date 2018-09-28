﻿using PX.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Acuminator.Tests.Tests.StaticAnalysis.LongOperationStart.Sources.PXGraph
{
    public class SMUserMaintExt : PXGraphExtension<SMUserMaint>
    {
        public IEnumerable users()
        {
            PXLongOperation.StartOperation(this, () => SyncUsers());
            return new PXSelect<PX.SM.Users>(Base).Select();
        }

        public static void SyncUsers()
        {
        }
    }

    public class SMUserMaint : PXGraph<SMUserMaint>
    {
        public PXSelect<PX.SM.Users> Users;
    }
}
