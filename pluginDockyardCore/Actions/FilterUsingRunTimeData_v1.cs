﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StructureMap;
using Core.Interfaces;
using Data.Entities;
using Data.Infrastructure;
using Data.Interfaces;
using Data.Interfaces.DataTransferObjects;
using Data.Interfaces.ManifestSchemas;
using Data.States;
using Data.States.Templates;
using Data.Wrappers;
using PluginBase.BaseClasses;
using PluginBase.Infrastructure;

namespace pluginDockyardCore.Actions
{
    public class FilterUsingRunTimeData_v1 : BasePluginAction
    {

        public FilterUsingRunTimeData_v1()
        {
           
        }

        /// <summary>
        /// Action processing infrastructure.
        /// </summary>
        public ActionProcessResultDTO Execute(ActionDTO curActionDTO)
        {
            var actionDO = AutoMapper.Mapper.Map<ActionDO>(curActionDTO);

            // Get parent action-list.
            var curActionList = ((ActionListDO) actionDO.ParentActivity);

            if (!curActionList.ProcessID.HasValue)
            {
                throw new ApplicationException("Action.ActionList.ProcessID is empty.");
            }

            // Find crate with id "Criteria Filter Conditions".
            var curCrateStorage = actionDO.CrateStorageDTO();
            var curFilterCrate = curCrateStorage.CrateDTO
                .FirstOrDefault(x => x.Id == "Criteria Filter Conditions");

            if (curFilterCrate == null)
            {
                throw new ApplicationException("No crate found with Id == \"Criteria Filter Conditions\"");
            }

            // Prepare envelope data.
            var curDocuSignEnvelope = new DocuSignEnvelope();
                // Should just change GetEnvelopeData to pass an EnvelopeDO.
            var curEnvelopeData = curDocuSignEnvelope.GetEnvelopeData(curDocuSignEnvelope);

            // Evaluate criteria using Contents json body of found Crate.
            var result = Evaluate(curFilterCrate.Contents,
                curActionList.ProcessID.Value, curEnvelopeData);

            // Process result.
            if (result)
            {
                actionDO.ActionState = ActionState.Active;
            }
            else
            {
                curActionList.Process.ProcessState = ProcessState.Completed;
            }

            return new ActionProcessResultDTO() {Success = true};
        }

        private bool Evaluate(string criteria, int processId, IEnumerable<EnvelopeDataDTO> envelopeData)
        {
            if (criteria == null)
                throw new ArgumentNullException("criteria");
            if (criteria == string.Empty)
                throw new ArgumentException("criteria is empty", "criteria");
            if (envelopeData == null)
                throw new ArgumentNullException("envelopeData");

            return Filter(criteria, processId, envelopeData.AsQueryable()).Any();
        }

        private IQueryable<EnvelopeDataDTO> Filter(string criteria, int processId,
            IQueryable<EnvelopeDataDTO> envelopeData)
        {
            if (criteria == null)
                throw new ArgumentNullException("criteria");
            if (criteria == string.Empty)
                throw new ArgumentException("criteria is empty", "criteria");
            if (envelopeData == null)
                throw new ArgumentNullException("envelopeData");

            EventManager.CriteriaEvaluationStarted(processId);
            var filterExpression = ParseCriteriaExpression(criteria, envelopeData);
            IQueryable<EnvelopeDataDTO> results =
                envelopeData.Provider.CreateQuery<EnvelopeDataDTO>(filterExpression);
            return results;
        }

        private Expression ParseCriteriaExpression<T>(string criteria, IQueryable<T> queryableData)
        {
            Expression criteriaExpression = null;
            ParameterExpression pe = Expression.Parameter(typeof (T), "p");
            JObject jCriteria = JObject.Parse(criteria);
            JArray jCriterions = (JArray) jCriteria.Property("criteria").Value;
            foreach (var jCriterion in jCriterions.OfType<JObject>())
            {

                var propName = (string) jCriterion.Property("field").Value;
                var propInfo = typeof (T).GetProperty(propName);
                var op = (string) jCriterion.Property("operator").Value;
                var value = ((JValue) jCriterion.Value<object>("value")).ToObject(propInfo.PropertyType);
                Expression left = Expression.Property(pe, propInfo);
                Expression right = Expression.Constant(value);
                Expression criterionExpression;
                switch (op)
                {
                    case "Equals":
                        criterionExpression = Expression.Equal(left, right);
                        break;
                    case "GreaterThan":
                        criterionExpression = Expression.GreaterThan(left, right);
                        break;
                    case "GreaterThanOrEquals":
                        criterionExpression = Expression.GreaterThanOrEqual(left, right);
                        break;
                    case "LessThan":
                        criterionExpression = Expression.LessThan(left, right);
                        break;
                    case "LessThanOrEquals":
                        criterionExpression = Expression.LessThanOrEqual(left, right);
                        break;
                    default:
                        throw new NotSupportedException(string.Format("Not supported operator: {0}", op));
                }

                if (criteriaExpression == null)
                    criteriaExpression = criterionExpression;
                else
                    criteriaExpression = Expression.AndAlso(criteriaExpression, criterionExpression);
            }

            if (criteriaExpression == null)
                criteriaExpression = Expression.Constant(true);

            var whereCallExpression = Expression.Call(
                typeof (Queryable),
                "Where",
                new[] {typeof (T)},
                queryableData.Expression,
                Expression.Lambda<Func<T, bool>>(criteriaExpression, new[] {pe}));
            return whereCallExpression;
        }

        /// <summary>
        /// Configure infrastructure.
        /// </summary>
        public CrateStorageDTO Configure(ActionDTO curActionDataPackageDTO)
        {

            return ProcessConfigurationRequest(curActionDataPackageDTO, ConfigurationEvaluator);
        }

        private CrateDTO CreateControlsCrate()
        {
            var fieldFilterPane = new FilterPaneFieldDefinitionDTO()
            {
                FieldLabel = "Criteria for Executing Actions",
                Type = "filterPane",
                Name = "Selected_Filter",
                Required = true,
                Source = new FieldSourceDTO
                {
                    Label = "Queryable Criteria",
                    ManifestType = "Standard Design-Time Fields"
                }
            };

            var controlsList = new List<FieldDefinitionDTO>() {fieldFilterPane};
            return PackControlsCrate(controlsList);
        }

        /// <summary>
        /// Looks for first Create with Id == "Standard Design-Time" among all upcoming Actions.
        /// </summary>
        protected override CrateStorageDTO InitialConfigurationResponse(ActionDTO curActionDTO)
        {
            if (curActionDTO.Id > 0)
            {
                //this conversion from actiondto to Action should be moved back to the controller edge
                using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
                {
                    ActionDO curActionDO = _action.MapFromDTO(curActionDTO);

                    StandardDesignTimeFieldsMS curUpstreamFields =
                        GetDesignTimeFields(curActionDO, GetCrateDirection.Upstream);

                    //2) Pack the merged fields into a new crate that can be used to populate the dropdownlistbox
                    CrateDTO queryFieldsCrate = _crate.CreateDesignTimeFieldsCrate(
                        "Queryable Criteria", curUpstreamFields);
                    
                    //build a controls crate to render the pane
                    CrateDTO configurationControlsCrate = CreateControlsCrate();

                    var curCrates = new List<CrateDTO>
                    {
                        queryFieldsCrate,
                        configurationControlsCrate
                    };

                    return AssembleCrateStorage(curCrates);
                }
            }
            else
            {
                throw new ArgumentException(
                    "Configuration requires the submission of an Action that has a real ActionId");
            }
        }

        /// <summary>
        /// ConfigurationEvaluator always returns Initial,
        /// since Initial and FollowUp phases are the same for current action.
        /// </summary>
        private ConfigurationRequestType ConfigurationEvaluator(ActionDTO curActionDataPackageDTO)
        {
            if (curActionDataPackageDTO.CrateStorage == null
                && curActionDataPackageDTO.CrateStorage.CrateDTO == null)
            {
                return ConfigurationRequestType.Initial;
            }


            var hasControlsCrate = curActionDataPackageDTO.CrateStorage.CrateDTO
                .Any(x => x.ManifestType == "Standard Configuration Controls"
                    && x.Label == "Configuration_Controls");

            var hasQueryFieldsCrate = curActionDataPackageDTO.CrateStorage.CrateDTO
                .Any(x => x.ManifestType == "Standard Design-Time Fields"
                    && x.Label == "Queryable Criteria");

            if (hasControlsCrate && hasQueryFieldsCrate)
            {
                return ConfigurationRequestType.Followup;
            }
            else
            {
                return ConfigurationRequestType.Initial;
            }
        }
    }
}
