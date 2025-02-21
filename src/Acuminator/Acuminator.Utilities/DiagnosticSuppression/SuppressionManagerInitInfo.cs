﻿using Acuminator.Utilities.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Acuminator.Utilities.DiagnosticSuppression
{
	public readonly struct SuppressionManagerInitInfo : IEquatable<SuppressionManagerInitInfo>
	{
		public string Path { get; }

		public bool GenerateSuppressionBase { get; }

		public SuppressionManagerInitInfo(string path, bool generateSuppressionBase)
		{
			Path = path.CheckIfNullOrWhiteSpace();
			GenerateSuppressionBase = generateSuppressionBase;
		}

		public override bool Equals(object obj) => obj is SuppressionManagerInitInfo initInfo
			? Equals(initInfo)
			: false;

		public bool Equals(SuppressionManagerInitInfo other) => 
			GenerateSuppressionBase == other.GenerateSuppressionBase && 
			string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase);

		public override int GetHashCode()
		{
			int hash = 17;

			unchecked
			{
				hash = 23 * hash + (Path?.ToUpperInvariant().GetHashCode() ?? 0);
				hash = 23 * hash + GenerateSuppressionBase.GetHashCode();
			}

			return hash;
		}
	}
}
