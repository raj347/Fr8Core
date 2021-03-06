﻿using System;
using System.Threading.Tasks;
using Fr8.Infrastructure.Data.Control;
using Fr8.Infrastructure.Data.Crates;
using Fr8.Infrastructure.Data.DataTransferObjects;
using Fr8.Infrastructure.Data.Managers;
using Fr8.Infrastructure.Data.States;
using Fr8.TerminalBase.BaseClasses;
using Fr8.TerminalBase.Errors;

namespace terminalFr8Core.Activities
{
    public class Set_Delay_v1 : ExplicitTerminalActivity
    {
        public static ActivityTemplateDTO ActivityTemplateDTO = new ActivityTemplateDTO
        {
            Id = new Guid("4059e018-d8a5-4927-9712-8430ffba0b73"),
            Name = "Set_Delay",
            Label = "Delay Action Processing",
            Version = "1",
            MinPaneWidth = 330,
            Type = ActivityType.Standard,
            Terminal = TerminalData.TerminalDTO,
            Categories = new[]
            {
                ActivityCategories.Process,
                TerminalData.ActivityCategoryDTO
            }
        };
        protected override ActivityTemplateDTO MyTemplate => ActivityTemplateDTO;

        private const int MinDurationSeconds = 10;
        private AlarmDTO CreateAlarm(TimeSpan duration)
                {
            if (duration.TotalSeconds == 0)
            {
                duration.Add(TimeSpan.FromSeconds(MinDurationSeconds));
            }
            return new AlarmDTO
            {
                ContainerId = ExecutionContext.ContainerId,
                StartTime = DateTime.UtcNow.Add(duration)
            };
        }
        private TimeSpan GetUserDefinedDelayDuration()
        {
            var manifestTypeDropdown = GetControl<Duration>("Delay_Duration");
            if (manifestTypeDropdown.Value == null)
            {
                throw new TerminalCodedException(TerminalErrorCode.PAYLOAD_DATA_MISSING, "Delay activity can't create a delay without a selected duration on design time");
            }
            return manifestTypeDropdown.Value;
        }

        private void CreateControls()
        {
            var duration = new Duration
            {
                Label = "Please enter delay duration",
                Name = "Delay_Duration"
            };

            AddControls(duration);
        }

        public Set_Delay_v1(ICrateManager crateManager)
            : base(crateManager)
        {
        }

        public override async Task Run()
        {
            //find our action state in operations crate
            var delayState = OperationalState.CallStack.GetLocalData<string>("Delay");
            //extract ActivityResponse type from result
            if (delayState == "suspended")
            {
                //this is second time we are being called. this means alarm has triggered
                Success();
                return;
            }

            //get user selected design time duration
            var delayDuration = GetUserDefinedDelayDuration();
            var alarmDTO = CreateAlarm(delayDuration);
            //post to hub to create an alarm
            await HubCommunicator.CreateAlarm(alarmDTO);

            OperationalState.CallStack.StoreLocalData("Delay", "suspended");

            RequestPlanExecutionSuspension();
            }

        public override Task Initialize()
        {
            CreateControls();

            return Task.FromResult(0);
        }

        public override Task FollowUp()
        {
            return Task.FromResult(0);
        }
    }
}