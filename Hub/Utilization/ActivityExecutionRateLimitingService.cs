﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Data.Interfaces;
using Data.States;
using log4net;
using StructureMap;
using Utilities.Configuration.Azure;
using Utilities.Interfaces;

namespace Hub.Utilization
{
    public class ActivityExecutionRateLimitingService : IDisposable, IActivityExecutionRateLimitingService
    {
        private const int MinimalRenewInterval = 1; // in seconds
        private const int DefaultOverheatingThreshold = 100; // executuions per unit
        private const int DefaultUserBanTime = 600; // in seconds

        private static readonly ILog Logger = Utilities.Logging.Logger.GetCurrentClassLogger();

        private readonly IUtilizationMonitoringService _utilizationMonitoringService;
        private readonly IUtilizationDataProvider _utilizationDataProvider;
        private readonly IPusherNotifier _pusherNotifier;
        private readonly Timer _utilizationRenewTimer;
        private readonly HashSet<string> _overheatingUsers = new HashSet<string>();
        private readonly int _overheatingThreshold;
        private readonly int _userBanTime; // in seconds
        private readonly object _sync = new object();
       
        private bool _isInitialized;

        public ActivityExecutionRateLimitingService(IUtilizationMonitoringService utilizationMonitoringService, IUtilizationDataProvider utilizationDataProvider, IPusherNotifier pusherNotifier)
        {
            var renewInterval = Math.Max(GetSetting ("UtilizationSateRenewInterval", utilizationMonitoringService.AggregationUnitDuration / 2000), MinimalRenewInterval);
            _overheatingThreshold = GetSetting("ActivityExecutionOverheatingThreshold", DefaultOverheatingThreshold);
            _userBanTime = GetSetting("OverheatedUserBanTime", DefaultUserBanTime);
            
            _utilizationMonitoringService = utilizationMonitoringService;
            _utilizationDataProvider = utilizationDataProvider;
            _pusherNotifier = pusherNotifier;

            _utilizationRenewTimer = new Timer(OnUtilizationStateRenewTick, this, renewInterval * 1000, renewInterval * 1000);
        }

        private static int GetSetting(string settingKey, int defaultValue)
        {
            var settingValueStr = CloudConfigurationManager.GetSetting(settingKey);
            int settingValue;

            if (string.IsNullOrWhiteSpace(settingValueStr) || !int.TryParse(settingValueStr, out settingValue))
            {
                settingValue = defaultValue;
            }

            return settingValue;
        }

        private static void OnUtilizationStateRenewTick(object state)
        {
            ((ActivityExecutionRateLimitingService) state).RenewUtilizationState();
        }

        // Load blocked users from the DB into local cache.
        private void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            _overheatingUsers.Clear();

            foreach (var overheatingUser in _utilizationDataProvider.GetOverheatingUsers())
            {
                _overheatingUsers.Add(overheatingUser);
            }
            
            _isInitialized = true;
            
        }

        // Update users states and sync DB chnages with local cache
        private void RenewUtilizationState()
        {
            OverheatingUsersUpdateResults overheatUpdateResults = null;

            lock (_sync)
            {
                Initialize();

                try
                {
                    overheatUpdateResults = _utilizationDataProvider.UpdateOverheatingUsers(_overheatingThreshold);

                    foreach (var userId in overheatUpdateResults.StartedOverheating)
                    {
                        _overheatingUsers.Add(userId);
                    }

                    foreach (var userId in overheatUpdateResults.StoppedOverheating)
                    {
                        _overheatingUsers.Remove(userId);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to update activities execution rates", ex);
                    // someting went wrong. Our local cache can be out of sync with the DB. 
                    // Reset initialize to reload the cache on the next request
                    _isInitialized = false;
                }
            }

            if (overheatUpdateResults != null)
            {
                using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
                {
                    foreach (var user in overheatUpdateResults.StartedOverheating)
                    {
                        NotifyUser(uow, user);
                    }
                }
            }
        }

        private void NotifyUser(IUnitOfWork uow, string user)
        {
            var userName = uow.UserRepository.GetQuery().Where(x => x.Id == user).Select(x => x.UserName).FirstOrDefault();

            if (userName != null)
            {
                _pusherNotifier.NotifyUser("You are running more Activities than your capacity right now. " +
                                           $"This Account will be prevented from processing Activities for the next {Math.Ceiling(_userBanTime / 60.0f)} minutes. " +
                                           "Contact support@fr8.co for assistance", NotificationChannel.GenericFailure, userName);
            }
        }

        public bool CheckActivityExecutionRate(string fr8AccountId)
        {
            lock (_sync)
            {
                Initialize();

                return !_overheatingUsers.Contains(fr8AccountId);
            }
        }

        public void Dispose()
        {
            _utilizationRenewTimer.Dispose();
        }
    }
}
