﻿using Data.Entities;
using System;
using System.Threading.Tasks;
using TerminalBase.BaseClasses;
using Fr8Data.Control;
using Fr8Data.Crates;
using Fr8Data.DataTransferObjects;

namespace terminalBaseTests.Actions
{
    public class terminalActivityMock_v1 : BaseTerminalActivity
    {
        public override Task Initialize()
        {
            AddCrateMethodInvoked("Initialize");
            return Task.FromResult(0);
            //return base.InitialConfigurationResponse(curActivityDO, authTokenDO);
        }

        public override Task FollowUp()
        {
            AddCrateMethodInvoked("FollowUp");
            return Task.FromResult(0);
            //return base.FollowupConfigurationResponse(curActivityDO, authTokenDO);
        }

        protected override Task Activate()
        {
            AddCrateMethodInvoked("Activate");
            return base.Activate();
        }

        protected override Task Deactivate()
        {
            AddCrateMethodInvoked("Deactivate");
            return base.Deactivate();
        }

        public Task OtherMethod()
        {
            AddCrateMethodInvoked("OtherMethod");
            return Task.FromResult(0);
            //return base.Deactivate(curActivityDO);
        }

        protected override ActivityTemplateDTO MyTemplate { get; }

        public override async Task Run()
        {
            AddCrateMethodInvoked("Run");
            //var processPayload = new PayloadDTO(Guid.NewGuid());
            //return await Task.FromResult(processPayload);
        }

        public async Task<PayloadDTO> ExecuteChildActivities(ActivityDO curActivityDO, Guid containerId, AuthorizationTokenDO authTokenDO)
        {
            AddCrateMethodInvoked("ExecuteChildActivities");
            var processPayload = new PayloadDTO(Guid.NewGuid());
            
            return await Task.FromResult(processPayload);
        }

        private void AddCrateMethodInvoked(string methodName)
        {
            Storage.Clear();
            Storage.Add(CreateControlsCrate(methodName));
        }

        private Crate CreateControlsCrate(string fieldName)
        {
            var fieldFilterPane = new TextBox
            {
                Label = fieldName,
                Name = "InvokedMethod"
            };

            return PackControlsCrate(fieldFilterPane);
        }

        public terminalActivityMock_v1() : base(false)
        {
        }
    }
}
