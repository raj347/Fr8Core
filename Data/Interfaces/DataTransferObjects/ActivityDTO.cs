﻿using System;
using Newtonsoft.Json;
using Data.Infrastructure.JsonNet;

namespace Data.Interfaces.DataTransferObjects
{
    public class ActivityDTO 
    {
        public string Label { get; set; }

        [JsonProperty("activityTemplate")]
        [JsonConverter(typeof(ActivityTemplateActivityConverter))]
        public ActivityTemplateDTO ActivityTemplate { get; set; }

        public Guid? RootPlanNodeId { get; set; }

        public Guid? ParentPlanNodeId { get; set; }

        public string CurrentView { get; set; }

        public int Ordering { get; set; }

        public Guid Id { get; set; }

        public CrateStorageDTO CrateStorage { get; set; }

        public ActivityDTO[] ChildrenActivities { get; set; }

        public AuthorizationTokenDTO AuthToken { get; set; }


        [JsonIgnore]
        public string Fr8AccountId { get; set; }

        [JsonProperty("documentation")]
        public string Documentation { get; set; }

    }
}
