﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Data.Entities;
using Data.Interfaces;
using Data.States;
using Data.Utility;
using Fr8.Infrastructure.Utilities.Configuration;
using Hub.Infrastructure;
using Hub.Interfaces;
using StructureMap;

namespace Hub.Services
{
    public class ActivityTemplate : IActivityTemplate
    {
        private readonly ITerminal _terminal;
        private readonly IActivityCategory _activityCategory;
        private readonly Dictionary<Guid, ActivityTemplateDO> _activityTemplates = new Dictionary<Guid, ActivityTemplateDO>();
        private bool _isInitialized;

        public bool IsATandTCacheDisabled
        {
            get;
            private set;
        }
        
        public ActivityTemplate(ITerminal terminal, IActivityCategory activityCategory)
        {
            IsATandTCacheDisabled = string.Equals(CloudConfigurationManager.GetSetting("DisableATandTCache"), "true", StringComparison.InvariantCultureIgnoreCase);
            _terminal = terminal;
            _activityCategory = activityCategory;
        }

        private void Initialize()
        {
            if (_isInitialized && !IsATandTCacheDisabled)
            {
                return;
            }

            lock (_activityTemplates)
            {
                if (_isInitialized && !IsATandTCacheDisabled)
                {
                    return;
                }

                if (IsATandTCacheDisabled)
                {
                    _activityTemplates.Clear();
                }

                LoadFromDb();

                _isInitialized = true;
            }
        }

        private void LoadFromDb()
        {
            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                var query = uow.ActivityTemplateRepository.GetQuery()
                    .Include(x => x.Categories)
                    .Include("Categories.ActivityCategory");
                foreach (var activityTemplate in query)
                {
                    _activityTemplates[activityTemplate.Id] = Clone(activityTemplate);
                }
            }
        }

        public ActivityTemplateInfo GetActivityTemplateInfo(string fullActivityTemplateName)
        {
            if (string.IsNullOrEmpty(fullActivityTemplateName))
            {
                throw new ApplicationException("Full ActivityTemplate name is not specified.");
            }

            string name;
            string version = null;

            var tokens = fullActivityTemplateName.Split('_');
            var lastToken = tokens[tokens.Length - 1];
            if (lastToken.StartsWith("v"))
            {
                int versionValue;
                if (Int32.TryParse(lastToken.Substring(1), out versionValue))
                {
                    version = versionValue.ToString();
                }
            }

            if (version != null)
            {
                name = fullActivityTemplateName.Substring(0, fullActivityTemplateName.Length - lastToken.Length - 1);
            }
            else
            {
                name = fullActivityTemplateName;
            }

            return new ActivityTemplateInfo()
            {
                Name = name,
                Version = version
            };
        }

        public ActivityTemplateDO[] GetAll()
        {
            Initialize();

            lock (_activityTemplates)
            {
                return _activityTemplates.Values.ToArray();
            }
        }

        public string GetTerminalUrl(Guid? curActivityTemplateId)
        {
            if (curActivityTemplateId == null)
            {
                return null;
            }

            Initialize();

            return GetByKey(curActivityTemplateId.Value).Terminal.Endpoint;
        }


        public bool TryGetByKey(Guid activityTemplateId, out ActivityTemplateDO activityTemplate)
        {
            Initialize();

            lock (_activityTemplates)
            {
                return _activityTemplates.TryGetValue(activityTemplateId, out activityTemplate);
            }
        }

        public ActivityTemplateDO GetByKey(Guid curActivityTemplateId)
        {
            Initialize();

            lock (_activityTemplates)
            {
                ActivityTemplateDO template;

                if (!_activityTemplates.TryGetValue(curActivityTemplateId, out template))
                {
                    throw new KeyNotFoundException(string.Format("Can't find activity template with id {0}", curActivityTemplateId));
                }

                return template;
            }
        }

        public IEnumerable<ActivityTemplateDO> GetQuery()
        {
            Initialize();

            lock (_activityTemplates)
            {
                var availableTerminalIds = _terminal.GetAll().Select(x => x.Id).ToList();

                return _activityTemplates.Values.Where(x => availableTerminalIds.Contains(x.TerminalId));
            }
        }

        private ActivityTemplateDO Clone(ActivityTemplateDO source)
        {
            var newTemplate = new ActivityTemplateDO();
            CopyPropertiesHelper.CopyProperties(source, newTemplate, false);
            newTemplate.Terminal = _terminal.GetByKey(source.TerminalId);
          
            if (source.Categories != null)
            {
                newTemplate.Categories = new List<ActivityCategorySetDO>();
                foreach (var acs in source.Categories)
                {
                    var newActivityCategory = new ActivityCategoryDO();
                    var activityCategory = _activityCategory.GetById(acs.ActivityCategoryId);
                    CopyPropertiesHelper.CopyProperties(activityCategory, newActivityCategory, false);

                    newTemplate.Categories.Add(new ActivityCategorySetDO()
                    {
                        ActivityTemplateId = newTemplate.Id,
                        ActivityTemplate = newTemplate,
                        ActivityCategoryId = newActivityCategory.Id,
                        ActivityCategory = newActivityCategory
                    });
                }
            }

            return newTemplate;
        }

        public void RemoveInactiveActivities(TerminalDO terminal, List<ActivityTemplateDO> activityTemplates)
        {
            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                var terminalActivitiesToBeRemoved = uow.ActivityTemplateRepository
                    .GetQuery()
                    .Where(x => x.TerminalId == terminal.Id)
                    .AsEnumerable()
                    .Where(x => !activityTemplates.Any(y => y.Name == x.Name && y.Version == x.Version))
                    .ToList();

                foreach (var activityToRemove in terminalActivitiesToBeRemoved)
                {
                    activityToRemove.ActivityTemplateState = ActivityTemplateState.Inactive;
                    _activityTemplates.Remove(activityToRemove.Id);
                }

                uow.SaveChanges();
            }          
        }

        public bool Exists(Guid activityTemplateId)
        {
            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                return uow.ActivityTemplateRepository.GetQuery().Any(x => x.Id == activityTemplateId);
            }
        }

        private List<ActivityCategorySetDO> ApplyActivityCategories(
            IUnitOfWork uow, 
            ActivityTemplateDO activityTemplate,
            List<ActivityCategoryDO> activityCategories)
        {
            // Remove previously registered ActivityCategorySets.
            var existingActivityCategorySets = uow.ActivityCategorySetRepository
                .GetQuery()
                .Where(x => x.ActivityTemplateId == activityTemplate.Id)
                .ToList();

            foreach (var activityCategorySet in existingActivityCategorySets)
            {
                uow.ActivityCategorySetRepository.Remove(activityCategorySet);
            }

            // Add new activityCategorySets.
            var activityCategorySets = new List<ActivityCategorySetDO>();

            foreach (var activityCategory in activityCategories)
            {
                var registeredActivityCategory = _activityCategory.RegisterOrUpdate(activityCategory);

                var activityCategorySet = new ActivityCategorySetDO()
                {
                    Id = Guid.NewGuid(),
                    ActivityTemplateId = activityTemplate.Id,
                    ActivityCategoryId = registeredActivityCategory.Id
                };

                activityCategorySets.Add(activityCategorySet);
                uow.ActivityCategorySetRepository.Add(activityCategorySet);
            }

            uow.SaveChanges();

            return activityCategorySets;
        }

        public void RegisterOrUpdate(ActivityTemplateDO activityTemplateDo)
        {
            if (activityTemplateDo == null)
            {
                return;
            }

            // validate values
            if (string.IsNullOrWhiteSpace(activityTemplateDo.Name))
            {
                throw new ArgumentOutOfRangeException("ActivityTemplate.Name can't be empty");
            }

            if (string.IsNullOrWhiteSpace(activityTemplateDo.Label))
            {
                throw new ArgumentOutOfRangeException("ActivityTemplate.Label can't be empty");
            }

            if (string.IsNullOrWhiteSpace(activityTemplateDo.Version))
            {
                throw new ArgumentOutOfRangeException("ActivityTemplate.Version can't be empty");
            }

            int tempVersion;
            if (!int.TryParse(activityTemplateDo.Version, out tempVersion))
            {
                throw new ArgumentOutOfRangeException($"ActivityTemplate.Version is not a valid integer value: {activityTemplateDo.Version}");
            }

            // we are going to change activityTemplateDo. It is not good to corrupt method's input parameters.
            // make a copy
            var clone = new ActivityTemplateDO();
            
            CopyPropertiesHelper.CopyProperties(activityTemplateDo, clone, true);
            
            clone.Terminal = activityTemplateDo.Terminal;

            // Create list of activity categories for current ActivityTemplate.
            var activityCategories = new List<ActivityCategoryDO>();
            if (activityTemplateDo.Categories != null)
            {
                foreach (var acs in activityTemplateDo.Categories)
                {
                    activityCategories.Add(acs.ActivityCategory);
                }
            }

            activityTemplateDo = clone;
            activityTemplateDo.Terminal = null; // otherwise we can add dupliacte terminals into the DB
            
            if (!IsATandTCacheDisabled)
            {
                Initialize();
            }

            lock (_activityTemplates)
            {
                using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
                {                    
                    // Try to extract existing ActivityTemplate.
                    var activity = uow.ActivityTemplateRepository.GetQuery()
                        // .Include(x => x.WebService)
                        .FirstOrDefault(t => t.Name == activityTemplateDo.Name
                            && t.TerminalId == activityTemplateDo.TerminalId
                            && t.Version == activityTemplateDo.Version);

                    // We're creating new ActivityTemplate.
                    if (activity == null)
                    {
                        if (activityTemplateDo.Id == Guid.Empty)
                        {
                            throw new ApplicationException("ActivityTemplate Id not specified");
                        }

                        activity = activityTemplateDo;
                        activityTemplateDo.Categories = null;

                        uow.ActivityTemplateRepository.Add(activityTemplateDo);
                        uow.SaveChanges();

                        activityTemplateDo.Categories = ApplyActivityCategories(uow, activityTemplateDo, activityCategories);
                    }
                    // We're updating existing ActivityTemplate.
                    else
                    {
                        // TODO: FR-4943, this breaks DEV's activity registration, commented out for now.
                        // if (activity.Id != activityTemplateDo.Id)
                        // {
                        //     throw new InvalidOperationException($"Existent activity with same Name ({activity.Name}) and Version ({activity.Version}) that we passed "
                        //     + $"has different Id. (ExistentId = {activity.Id}. Passed Id = {activityTemplateDo.Id}. Changing of activity template Id is not possible. If you need to have another Id please update the version number or create new activity template");
                        // }

                        // This is for updating activity template
                        CopyPropertiesHelper.CopyProperties(activityTemplateDo, activity, false, x => x.Name != "Id");
                        activity.ActivityTemplateState = ActivityTemplateState.Active;

                        uow.SaveChanges();

                        activity.Categories = ApplyActivityCategories(uow, activity, activityCategories);
                    }

                    _activityTemplates[activity.Id] = Clone(activity);
                }
            }
        }

        /// <summary>
        /// Returns ActivityTemplate by it's name.
        /// For example GetByName(uow, "AddPayloadManually_v1").
        /// </summary>
        public ActivityTemplateDO GetByName(IUnitOfWork uow, string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ApplicationException("Invalid ActivityTemplate name");
            }

            var tokens = name.Split('_');
            if (tokens.Length < 2)
            {
                throw new ApplicationException("Invalid ActivityTemplate name");
            }

            var versionToken = tokens[tokens.Length - 1];

            if (versionToken == null || versionToken.Length < 2)
            {
                throw new ApplicationException("Invalid ActivityTemplate name");
            }

            var namePart = string.Join("_", tokens.Take(tokens.Length - 1).ToArray());
            var versionPart = versionToken.Substring(1);

            return GetByNameAndVersion(namePart, versionPart);
        }

        /// <summary>
        /// Returns ActivityTemplate by it's name and version.
        /// For example GetByNameAndVersion(uow, "AddPayloadManually", "1").
        /// </summary>
        public ActivityTemplateDO GetByNameAndVersion(string name, string version)
        {
            Initialize();

            lock (_activityTemplates)
            {
                return _activityTemplates.Values.FirstOrDefault(x => x.Name == name && x.Version == version);
            }
        }
    }
}
