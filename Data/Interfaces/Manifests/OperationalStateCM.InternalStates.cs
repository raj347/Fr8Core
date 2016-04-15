﻿using System;

namespace Data.Interfaces.Manifests
{
    partial class OperationalStateCM
    {
        public class HistoryElement
        {
            public string Description { get; set; }
        }

        public class LoopStatus
        {
            public int Index { get; set; }
            public string CrateManifest { get; set; }
            public string Label { get; set; }
        }

        public class BranchStatus
        {
            public string Id { get; set; }
            public int Count { get; set; }
            public DateTime LastBranchTime { get; set; }
        }
    }
}
