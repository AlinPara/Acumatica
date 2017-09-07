﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace PX.Analyzers.Coloriser
{
	public static class StringExtensions
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsNullOrWhiteSpace(this string str) => string.IsNullOrWhiteSpace(str);
	}

	public static class ConcurrentBagExtensions
	{
		public static void Clear<T>(this ConcurrentBag<T> bag)
		{
			if (bag == null)
				return;

			T someItem;

			while (!bag.IsEmpty)
			{
				bag.TryTake(out someItem);
			}
		}
	}
}
