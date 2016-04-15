﻿using System;
using Data.Constants;
using Hub.Managers;

namespace DockyardTest.Services.Container
{
    class LooperActivityMock : ActivityMockBase
    {
        private readonly int _count;

        public LooperActivityMock(ICrateManager crateManager, int count) 
            : base(crateManager)
        {
            _count = count;
        }

        protected override void Run(Guid id, ActivityExecutionMode executionMode)
        {
            if (executionMode == ActivityExecutionMode.InitialRun)
            {
                int? index = OperationalState.CallStack.GetLocalData<int?>("Loop");

                if (index == null)
                {
                    index = 0;
                }
                else
                {
                    index++;
                }

                OperationalState.CallStack.StoreLocalData("Loop", index);

                if (index >= _count)
                {
                    RequestSkipChildren();
                }
            }
            else
            {
                RequestJumpToActivity(id);
            }
        }
    }
}