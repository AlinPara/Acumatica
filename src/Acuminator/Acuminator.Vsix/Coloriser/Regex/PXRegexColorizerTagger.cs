﻿#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Acuminator.Utilities;
using Acuminator.Utilities.Common;
using Acuminator.Utilities.Roslyn;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;

namespace Acuminator.Vsix.Coloriser
{
	internal class PXRegexColorizerTagger : PXColorizerTaggerBase
	{
		protected internal override bool UseAsyncTagging => false;

		public override TaggerType TaggerType => TaggerType.RegEx;

		private readonly TagsCacheSync<IClassificationTag> _classificationTagsCache = new TagsCacheSync<IClassificationTag>();

		protected internal override ITagsCache<IClassificationTag> ClassificationTagsCache => _classificationTagsCache;

		private readonly TagsCacheSync<IOutliningRegionTag> _outliningTagsCache = new TagsCacheSync<IOutliningRegionTag>(capacity: 0);

		protected internal override ITagsCache<IOutliningRegionTag> OutliningsTagsCache => _outliningTagsCache;


		private readonly ConcurrentBag<ITagSpan<IClassificationTag>> _tagsBag = new ConcurrentBag<ITagSpan<IClassificationTag>>();

		internal PXRegexColorizerTagger(ITextBuffer buffer, PXColorizerTaggerProvider aProvider, bool subscribeToSettingsChanges,
										bool useCacheChecking) :
								   base(buffer, aProvider, subscribeToSettingsChanges, useCacheChecking)
		{
		}

		protected internal async override Task<IEnumerable<ITagSpan<IClassificationTag>>> GetTagsAsyncImplementationAsync(ITextSnapshot snapshot,
																											   CancellationToken cancellationToken)
		{
			var taggingInfo = await Task.Run(() => GetTagsSynchronousImplementation(snapshot))
										.TryAwait();

			if (!taggingInfo.IsSuccess)
				return [];

			return taggingInfo.Result ?? [];
		}

		protected internal override IEnumerable<ITagSpan<IClassificationTag>> GetTagsSynchronousImplementation(ITextSnapshot snapshot)
		{
			GetTagsFromSnapshot(snapshot);
			_classificationTagsCache.AddTags(_tagsBag);
			ClassificationTagsCache.CompleteProcessing();
			OutliningsTagsCache.CompleteProcessing();
			return ClassificationTagsCache;
		}

		protected internal override void ResetCacheAndFlags(ITextSnapshot newCache)
		{
			base.ResetCacheAndFlags(newCache);
			_tagsBag.Clear();
		}

		private void GetTagsFromSnapshot(ITextSnapshot newSnapshot)
		{
			string wholeText = newSnapshot.GetText();
			var matches = RegExpressions.BQLSelectCommandRegex
										.Matches(wholeText)
										.OfType<Match>()
										.Where(bqlCommandMatch => !string.IsNullOrWhiteSpace(bqlCommandMatch.Value));

			Parallel.ForEach(matches,
							 bqlCommandMatch => GetTagsFromBQLCommand(newSnapshot, bqlCommandMatch.Value, bqlCommandMatch.Index));
		}

		private void GetTagsFromBQLCommand(ITextSnapshot newSnapshot, string bqlCommand, int offset)
		{
			int lastAngleBraces = bqlCommand.LastIndexOf('>');
			bqlCommand = bqlCommand.Substring(0, lastAngleBraces + 1);
			int lastAngleBraceIndex = bqlCommand.LastIndexOf('>');

			if (lastAngleBraceIndex >= 0)
				bqlCommand = bqlCommand.Substring(0, lastAngleBraceIndex + 1);

			int firstAngleBraceIndex = bqlCommand.IndexOf('<');

			if (firstAngleBraceIndex < 0)
				return;

			string selectOp = bqlCommand.Substring(0, firstAngleBraceIndex);
			GetSelectCommandTag(newSnapshot, bqlCommand, offset);

			GetBQLOperandTags(newSnapshot, bqlCommand, offset);
			GetDacWithFieldTags(newSnapshot, bqlCommand, offset);
			GetSingleDacAndConstantTags(newSnapshot, bqlCommand, offset);
			GetBqlParameterTags(newSnapshot, bqlCommand, offset);
		}

		private void GetBQLOperandTags(ITextSnapshot newSnapshot, string bqlCommand, int offset)
		{
			var matches = RegExpressions.DacOperandRegex.Matches(bqlCommand);

			foreach (Match bqlOperandMatch in matches.OfType<Match>().Skip(1))
			{
				string bqlOperand = bqlOperandMatch.Value.TrimEnd('<');
				CreateTag(newSnapshot, bqlOperandMatch, offset, bqlOperand, Provider[PXCodeType.BqlOperator]);
			}
		}

		private void GetSelectCommandTag(ITextSnapshot newSnapshot, string selectOp, int offset)
		{
			Span span = new Span(offset, selectOp.Length);
			SnapshotSpan snapshotSpan = new SnapshotSpan(newSnapshot, span);
			var tag = new TagSpan<IClassificationTag>(snapshotSpan, new ClassificationTag(Provider[PXCodeType.BqlOperator]));
			_tagsBag.Add(tag);
		}

		private void GetBqlParameterTags(ITextSnapshot newSnapshot, string bqlCommand, int offset)
		{
			var matches = RegExpressions.BQLParametersRegex.Matches(bqlCommand);

			foreach (Match bqlParamMatch in matches)
			{
				string bqlParam = bqlParamMatch.Value;
				CreateTag(newSnapshot, bqlParamMatch, offset, bqlParam, Provider[PXCodeType.BqlParameter]);
			}
		}

		private void GetSingleDacAndConstantTags(ITextSnapshot newSnapshot, string bqlCommand, int offset)
		{
			var matches = RegExpressions.DacOrConstantRegex.Matches(bqlCommand);

			foreach (Match dacOrConstMatch in matches)
			{
				string dacOrConst = dacOrConstMatch.Value.Trim(',', '<', '>');
				CreateTag(newSnapshot, dacOrConstMatch, offset, dacOrConst, Provider[PXCodeType.Dac]);
			}
		}

		private void GetDacWithFieldTags(ITextSnapshot newSnapshot, string bqlCommand, int offset)
		{
			var matches = RegExpressions.DacWithFieldRegex.Matches(bqlCommand);

			foreach (Match dacWithFieldMatch in matches)
			{
				string[] dacParts = dacWithFieldMatch.Value.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

				if (dacParts.Length != 2)
					continue;

				GetDacTag(newSnapshot, dacWithFieldMatch, offset, dacParts);
				GetFieldTag(newSnapshot, dacWithFieldMatch, offset, dacParts);
			}
		}

		private void GetDacTag(ITextSnapshot newSnapshot, Match match, int offset, string[] dacParts)
		{
			string dacPart = dacParts[0].TrimStart(',', '<');
			CreateTag(newSnapshot, match, offset, dacPart, Provider[PXCodeType.Dac]);
		}

		private void GetFieldTag(ITextSnapshot newSnapshot, Match match, int offset, string[] dacParts)
		{
			string fieldPart = dacParts[1].TrimEnd(',', '>');
			CreateTag(newSnapshot, match, offset, fieldPart, Provider[PXCodeType.DacField]);
		}

		private void CreateTag(ITextSnapshot newSnapshot, Match match, int offset, string tagContent, IClassificationType? classType)
		{
			if (classType == null)
				return;

			int startIndex = offset + match.Index + match.Value.IndexOf(tagContent);
			var span = new Span(startIndex, tagContent.Length);
			var snapshotSpan = new SnapshotSpan(newSnapshot, span);
			var tag = new TagSpan<IClassificationTag>(snapshotSpan, new ClassificationTag(classType));

			_tagsBag.Add(tag);
		}
	}
}
