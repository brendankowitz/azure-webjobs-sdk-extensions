﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.Azure.WebJobs.Host;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Common
{
    public class TestTraceWriter : TraceWriter
    {
        public TestTraceWriter() : this(TraceLevel.Verbose)
        {
        }

        public TestTraceWriter(TraceLevel level) : base(level)
        {
            Traces = new Collection<TraceEvent>();
        }

        public Collection<TraceEvent> Traces { get; private set; }

        public override void Trace(TraceEvent traceEvent)
        {
            Traces.Add(traceEvent);
        }
    }
}
