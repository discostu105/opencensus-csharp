﻿// <copyright file="MutableViewData.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

namespace OpenCensus.Stats
{
    using System;
    using System.Collections.Generic;
    using OpenCensus.Common;
    using OpenCensus.Stats.Aggregations;
    using OpenCensus.Tags;

    internal abstract class MutableViewData
    {
        internal static readonly ITagValue UnknownTagValue = null;

        internal static readonly Timestamp ZeroTimestamp = Timestamp.Create(0, 0);

        private const long MillisPerSecond = 1000L;
        private const long NanosPerMilli = 1000 * 1000;

        protected MutableViewData(IView view)
        {
            this.View = view;
        }

        internal IView View { get; }

        private static Func<ISum, MutableAggregation> CreateMutableSum { get; } = (s) => { return MutableSum.Create(); };

        private static Func<ICount, MutableAggregation> CreateMutableCount { get; } = (s) => { return MutableCount.Create(); };

        private static Func<IMean, MutableAggregation> CreateMutableMean { get; } = (s) => { return MutableMean.Create(); };

        private static Func<ILastValue, MutableAggregation> CreateMutableLastValue { get; } = (s) => { return MutableLastValue.Create(); };

        private static Func<IDistribution, MutableAggregation> CreateMutableDistribution { get; } = (s) => { return MutableDistribution.Create(s.BucketBoundaries); };

        private static Func<IAggregation, MutableAggregation> ThrowArgumentException { get; } = (s) => { throw new ArgumentException(); };

        private static Func<MutableCount, IAggregationData> CreateCountData { get; } = (s) => { return CountData.Create(s.Count); };

        private static Func<MutableMean, IAggregationData> CreateMeanData { get; } = (s) => { return MeanData.Create(s.Mean, s.Count, s.Min, s.Max); };

        private static Func<MutableDistribution, IAggregationData> CreateDistributionData { get; } = (s) =>
        {
            List<long> boxedBucketCounts = new List<long>();
            foreach (long bucketCount in s.BucketCounts)
            {
                boxedBucketCounts.Add(bucketCount);
            }

            return DistributionData.Create(
                s.Mean,
                s.Count,
                s.Min,
                s.Max,
                s.SumOfSquaredDeviations,
                boxedBucketCounts);
        };

        internal static IDictionary<ITagKey, ITagValue> GetTagMap(ITagContext ctx)
        {
            if (ctx is TagContext)
            {
                return ((TagContext)ctx).Tags;
            }
            else
            {
                IDictionary<ITagKey, ITagValue> tags = new Dictionary<ITagKey, ITagValue>();
                foreach (var tag in ctx)
                {
                    tags.Add(tag.Key, tag.Value);
                }

                return tags;
            }
        }

        internal static IList<ITagValue> GetTagValues(IDictionary<ITagKey, ITagValue> tags, IList<ITagKey> columns)
        {
            IList<ITagValue> tagValues = new List<ITagValue>(columns.Count);

            // Record all the measures in a "Greedy" way.
            // Every view aggregates every measure. This is similar to doing a GROUPBY view’s keys.
            for (int i = 0; i < columns.Count; ++i)
            {
                ITagKey tagKey = columns[i];
                if (!tags.ContainsKey(tagKey))
                {
                    // replace not found key values by null.
                    tagValues.Add(UnknownTagValue);
                }
                else
                {
                    tagValues.Add(tags[tagKey]);
                }
            }

            return tagValues;
        }

        // Returns the milliseconds representation of a Duration.
        internal static long ToMillis(Duration duration)
        {
            return (duration.Seconds * MillisPerSecond) + (duration.Nanos / NanosPerMilli);
        }

        internal static MutableAggregation CreateMutableAggregation(IAggregation aggregation)
        {
            return aggregation.Match(
                CreateMutableSum,
                CreateMutableCount,
                CreateMutableMean,
                CreateMutableDistribution,
                CreateMutableLastValue,
                ThrowArgumentException);
        }

        internal static IAggregationData CreateAggregationData(MutableAggregation aggregation, IMeasure measure)
        {
            return aggregation.Match<IAggregationData>(
                (msum) =>
                {
                    return measure.Match<IAggregationData>(
                        (mdouble) =>
                        {
                            return SumDataDouble.Create(msum.Sum);
                        },
                        (mlong) =>
                        {
                            return SumDataLong.Create((long)Math.Round(msum.Sum));
                        },
                        (invalid) =>
                        {
                            throw new ArgumentException();
                        });
                },
                CreateCountData,
                CreateMeanData,
                CreateDistributionData,
                (mlval) =>
                {
                    return measure.Match<IAggregationData>(
                        (mdouble) =>
                        {
                            return LastValueDataDouble.Create(mlval.LastValue);
                        },
                        (mlong) =>
                        {
                            if (double.IsNaN(mlval.LastValue))
                            {
                                return LastValueDataLong.Create(0);
                            }

                            return LastValueDataLong.Create((long)Math.Round(mlval.LastValue));
                        },
                        (invalid) =>
                        {
                            throw new ArgumentException();
                        });
                });
        }

        // Covert a mapping from TagValues to MutableAggregation, to a mapping from TagValues to
        // AggregationData.
        internal static IDictionary<TagValues, IAggregationData> CreateAggregationMap(IDictionary<TagValues, MutableAggregation> tagValueAggregationMap, IMeasure measure)
        {
            IDictionary<TagValues, IAggregationData> map = new Dictionary<TagValues, IAggregationData>();
            foreach (var entry in tagValueAggregationMap)
            {
                map.Add(entry.Key, CreateAggregationData(entry.Value, measure));
            }

            return map;
        }

        internal static MutableViewData Create(IView view, DateTimeOffset start)
        {
            return new CumulativeMutableViewData(view, start);
        }

        /** Record double stats with the given tags. */
        internal abstract void Record(ITagContext context, double value, DateTimeOffset timestamp);

        /** Record long stats with the given tags. */
        internal void Record(ITagContext tags, long value, DateTimeOffset timestamp)
        {
            // TODO(songya): shall we check for precision loss here?
            this.Record(tags, (double)value, timestamp);
        }

        /** Convert this {@link MutableViewData} to {@link ViewData}. */
        internal abstract IViewData ToViewData(DateTimeOffset now, StatsCollectionState state);

        // Clear recorded stats.
        internal abstract void ClearStats();

        // Resume stats collection, and reset Start Timestamp (for CumulativeMutableViewData), or refresh
        // bucket list (for InternalMutableViewData).
        internal abstract void ResumeStatsCollection(DateTimeOffset now);
    }
}
