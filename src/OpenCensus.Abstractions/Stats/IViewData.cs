﻿// <copyright file="IViewData.cs" company="OpenCensus Authors">
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
    using OpenCensus.Tags;

    /// <summary>
    /// Result data of the view aggregation.
    /// </summary>
    public interface IViewData
    {
        /// <summary>
        /// Gets the view this data calculated for.
        /// </summary>
        IView View { get; }

        /// <summary>
        /// Gets the aggregation data grouped by combination of tag values associated with this view data.
        /// </summary>
        IDictionary<TagValues, IAggregationData> AggregationMap { get; }

        /// <summary>
        /// Gets the timestamp of a start of this aggregation.
        /// </summary>
        DateTimeOffset Start { get; }

        /// <summary>
        /// Gets the timestamp of the end of this aggregation.
        /// </summary>
        DateTimeOffset End { get; }
    }
}
