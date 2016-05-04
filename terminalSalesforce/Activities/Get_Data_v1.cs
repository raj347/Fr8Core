﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fr8Data.Constants;
using Fr8Data.Control;
using Fr8Data.Crates;
using Fr8Data.DataTransferObjects;
using Fr8Data.Manifests;
using Fr8Data.States;
using Newtonsoft.Json;
using ServiceStack;
using StructureMap;
using TerminalBase.BaseClasses;
using terminalSalesforce.Infrastructure;

namespace terminalSalesforce.Actions
{
    public class Get_Data_v1 : BaseSalesforceTerminalActivity<Get_Data_v1.ActivityUi>
    {
        public class ActivityUi : StandardConfigurationControlsCM
        {
            public DropDownList SalesforceObjectSelector { get; set; }

            public QueryBuilder SalesforceObjectFilter { get; set; }

            public ActivityUi()
            {
                SalesforceObjectSelector = new DropDownList
                {
                    Name = nameof(SalesforceObjectSelector),
                    Label = "Get all objects of type:",
                    Required = true,
                    Events = new List<ControlEvent> {  ControlEvent.RequestConfig }
                };
                SalesforceObjectFilter = new QueryBuilder
                {
                    Name = nameof(SalesforceObjectFilter),
                    Label = "That meet the following conditions:",
                    Required = true,
                    Source = new FieldSourceDTO
                    {
                        Label = QueryFilterCrateLabel,
                        ManifestType = CrateManifestTypes.StandardDesignTimeFields
                    }
                };
                Controls.Add(SalesforceObjectSelector);
                Controls.Add(SalesforceObjectFilter);
            }
        }
        //NOTE: this label must be the same as the one expected in QueryBuilder.ts
        public const string QueryFilterCrateLabel = "Queryable Criteria";

        public const string RuntimeDataCrateLabel = "Table from Salesforce Get Data";
        public const string PayloadDataCrateLabel = "Payload from Salesforce Get Data";

        public const string SalesforceObjectFieldsCrateLabel = "Salesforce Object Fields";

        private readonly ISalesforceManager _salesforceManager;

        public Get_Data_v1()
        {
            _salesforceManager = ObjectFactory.GetInstance<ISalesforceManager>();
            ActivityName = "Get Data from Salesforce";
        }

        protected override Task Initialize(RuntimeCrateManager runtimeCrateManager)
        {
            ConfigurationControls.SalesforceObjectSelector.ListItems = _salesforceManager
                .GetSalesforceObjectTypes()
                .Select(x => new ListItem() { Key = x.Key, Value = x.Key })
                .ToList();
            runtimeCrateManager.MarkAvailableAtRuntime<StandardTableDataCM>(RuntimeDataCrateLabel);
            return Task.FromResult(true);
        }

        protected override async Task Configure(RuntimeCrateManager runtimeCrateManager)
        {
            //If Salesforce object is empty then we should clear filters as they are no longer applicable
            var selectedObject = ConfigurationControls.SalesforceObjectSelector.selectedKey;
            if (string.IsNullOrEmpty(selectedObject))
            {
                CurrentActivityStorage.RemoveByLabel(QueryFilterCrateLabel);
                CurrentActivityStorage.RemoveByLabel(SalesforceObjectFieldsCrateLabel);
                this[nameof(ActivityUi.SalesforceObjectSelector)] = selectedObject;
                return;
            }
            //If the same object is selected we shouldn't do anything
            if (selectedObject == this[nameof(ActivityUi.SalesforceObjectSelector)])
            {
                return;
            }
            //Prepare new query filters from selected object properties
            var selectedObjectProperties = await _salesforceManager
                .GetProperties(selectedObject.ToEnum<SalesforceObjectType>(), AuthorizationToken);
            var queryFilterCrate = Crate<FieldDescriptionsCM>.FromContent(
                QueryFilterCrateLabel,
                new FieldDescriptionsCM(selectedObjectProperties),
                AvailabilityType.Configuration);
            CurrentActivityStorage.ReplaceByLabel(queryFilterCrate);

            this[nameof(ActivityUi.SalesforceObjectSelector)] = selectedObject;
            //Publish information for downstream activities
            runtimeCrateManager.MarkAvailableAtRuntime<StandardTableDataCM>(RuntimeDataCrateLabel);
        }

        protected override async Task RunCurrentActivity()
        {
            var salesforceObject = ConfigurationControls.SalesforceObjectSelector.selectedKey;
            if (string.IsNullOrEmpty(salesforceObject))
            {
                throw new ActivityExecutionException(
                    "No Salesforce object is selected", 
                    ActivityErrorCode.DESIGN_TIME_DATA_MISSING);
            }
            var salesforceObjectFields = CurrentActivityStorage
                .FirstCrate<FieldDescriptionsCM>(x => x.Label == QueryFilterCrateLabel)
                .Content
                .Fields
                .Select(x => x.Key);

            var filterValue = ConfigurationControls.SalesforceObjectFilter.Value;
            var filterDataDTO = JsonConvert.DeserializeObject<List<FilterConditionDTO>>(filterValue);
            //If without filter, just get all selected objects
            //else prepare SOQL query to filter the objects based on the filter conditions
            var parsedCondition = string.Empty;
            if (filterDataDTO.Count > 0)
            {
                parsedCondition = ParseConditionToText(filterDataDTO);
            }

            var resultObjects = await _salesforceManager
                .Query(
                    salesforceObject.ToEnum<SalesforceObjectType>(),
                    salesforceObjectFields,
                    parsedCondition,
                    AuthorizationToken
                );

            CurrentPayloadStorage.Add(
                Crate<StandardPayloadDataCM>
                    .FromContent(
                        PayloadDataCrateLabel,
                        resultObjects.ToPayloadData(),
                        AvailabilityType.RunTime
                    )
            );

            CurrentPayloadStorage.Add(
                Crate<StandardTableDataCM>
                    .FromContent(
                        RuntimeDataCrateLabel,
                        resultObjects,
                        AvailabilityType.RunTime
                    )
                );
        }
    }
}