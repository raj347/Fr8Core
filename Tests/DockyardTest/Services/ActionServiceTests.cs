﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Helpers;
using AutoMapper;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using StructureMap;
using Data.Entities;
using Data.Infrastructure;
using Data.Interfaces;
using Data.Interfaces.DataTransferObjects;
using Data.States;
using Hub.Interfaces;
using Hub.Managers;
using Hub.Managers.APIManagers.Transmitters.Plugin;
using Hub.Managers.APIManagers.Transmitters.Restful;
using Hub.Services;
using TerminalBase.BaseClasses;
using UtilitiesTesting;
using UtilitiesTesting.Fixtures;
using Action = Hub.Services.Action;


namespace DockyardTest.Services
{
    [TestFixture]
    [Category("ActionService")]
    public class ActionServiceTests : BaseTest
    {
        private IAction _action;
        private ICrateManager _crate;
        private IUnitOfWork _uow;
        private FixtureData _fixtureData;
        private readonly IEnumerable<ActivityTemplateDO> _pr1Activities = new List<ActivityTemplateDO>() { new ActivityTemplateDO() { Name = "Write", Version = "1.0" }, new ActivityTemplateDO() { Name = "Read", Version = "1.0" } };
        private readonly IEnumerable<ActivityTemplateDO> _pr2Activities = new List<ActivityTemplateDO>() { new ActivityTemplateDO() { Name = "SQL Write", Version = "1.0" }, new ActivityTemplateDO() { Name = "SQL Read", Version = "1.0" } };
        private bool _eventReceived;
        private BasePluginAction _basePluginAction;
        private IPlugin _plugin;
        private Mock<IPluginTransmitter> PluginTransmitterMock
        {
            get { return Mock.Get(ObjectFactory.GetInstance<IPluginTransmitter>()); }
        }

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            _action = ObjectFactory.GetInstance<IAction>();
            _crate = ObjectFactory.GetInstance<ICrateManager>();
            _uow = ObjectFactory.GetInstance<IUnitOfWork>();
            _fixtureData = new FixtureData(_uow);
            _eventReceived = false;
            _basePluginAction = new BasePluginAction();
            _plugin = ObjectFactory.GetInstance<Plugin>();
        }
        
        // DO-1214
//        [Test]
//        public async void Action_Configure_ExistingActionShouldBeUpdatedWithNewAction()
//        {
//            //Arrange
//            ActionDO curActionDO = FixtureData.IntegrationTestAction();
//            UpdateDatabase(curActionDO);
//
//            ActionDTO actionDto = Mapper.Map<ActionDTO>(curActionDO);
//
//            //set the new name
//            actionDto.Name = "NewActionFromServer";
//            PluginTransmitterMock.Setup(rc => rc.CallActionAsync<ActionDTO>(It.IsAny<string>(), It.IsAny<ActionDTO>()))
//                .Returns(() => Task.FromResult(actionDto));
//
//            //Act
//            var returnedAction = await _action.Configure(curActionDO);
//
//            //Assert
//            //get the action from the database
//            var updatedActionDO = _uow.ActionRepository.GetByKey(returnedAction.Id);
//            Assert.IsNotNull(updatedActionDO);
//            Assert.AreEqual(updatedActionDO.Name, actionDto.Name);
//        }

        // DO-1214
//        [Test]
//        public void UpdateCurrentActivity_ShouldUpdateCurrentActivity()
//        {
//            var curActionList = FixtureData.TestActionList2();
//
//            // Set current activity
//            curActionList.CurrentActivity = curActionList.Activities.Single(a => a.Id == 1);
//            curActionList.Id = curActionList.CurrentActivity.Id;
//
//            Assert.AreEqual(1, curActionList.CurrentActivity.Id);
//
//            using (IUnitOfWork uow = ObjectFactory.GetInstance<IUnitOfWork>())
//            {
//                uow.ActionListRepository.Add(curActionList);
//                uow.SaveChanges();
//
//                _action.UpdateCurrentActivity(curActionList.CurrentActivityID.Value, uow);
//
//                Assert.AreEqual(2, curActionList.CurrentActivity.Id);
//
//                // Check when current action is the only action in action list (should set null)
//                curActionList.Activities.RemoveAt(1);
//                uow.SaveChanges();
//
//                _action.UpdateCurrentActivity(curActionList.CurrentActivityID.Value, uow);
//                Assert.AreEqual(null, curActionList.CurrentActivity);
//            }
//        }




        [Test]
        [ExpectedException(ExpectedException = typeof(ArgumentNullException))]
        public async void Action_Configure_WithNullActionTemplate_ThrowsArgumentNullException()
        {
            var _service = new Action();
            await _service.Configure(null, null);
        }

        [Test]
        public void CanCRUDActions()
        {
            ActionDO origActionDO;

            using (IUnitOfWork uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                var route = FixtureData.TestRoute1();
                uow.RouteRepository.Add(route);

                var subroute = FixtureData.TestSubrouteDO1();
                uow.RouteNodeRepository.Add(subroute);

                origActionDO = new FixtureData(uow).TestAction3();

                origActionDO.IsTempId = true;
                origActionDO.ParentRouteNodeId = subroute.Id;

                uow.ActivityTemplateRepository.Add(origActionDO.ActivityTemplate);
                uow.SaveChanges();
            }

            IAction action = new Action();

            //Add
            action.SaveOrUpdateAction(origActionDO);

            //Get
            var actionDO = action.GetById(origActionDO.Id);
            Assert.AreEqual(origActionDO.Name, actionDO.Name);
            Assert.AreEqual(origActionDO.Id, actionDO.Id);
            Assert.AreEqual(origActionDO.CrateStorage, actionDO.CrateStorage);

            Assert.AreEqual(origActionDO.Ordering, actionDO.Ordering);

            ISubroute subRoute = new Subroute();
            //Delete
            subRoute.DeleteAction(null, actionDO.Id, true);
        }

        [Test]
        public void ActionWithNestedUpdated_StructureUnchanged()
        {
            var tree = FixtureData.CreateTestActionTreeWithOnlyActionDo();
            var updatedTree = FixtureData.CreateTestActionTreeWithOnlyActionDo();

            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                Visit(tree, x => uow.ActionRepository.Add(x));
                Visit(updatedTree, x => x.Name = string.Format("We were here {0}", x.Id));

                _action.SaveOrUpdateAction(uow, updatedTree);

                var result = uow.ActionRepository.GetByKey(tree.Id);
                Compare(updatedTree, result, (r, a) =>
                {
                    if (r.Name != a.Name)
                    {
                        throw new Exception("Update failed");
                    }
                });
            }
        }

        [Test]
        public void ActionWithNestedUpdated_RemoveElements()
        {
            var tree = FixtureData.CreateTestActionTreeWithOnlyActionDo();
            var updatedTree = FixtureData.CreateTestActionTreeWithOnlyActionDo();


            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                Visit(tree, x => uow.ActionRepository.Add(x));

                int removeCounter = 0;

                Visit(updatedTree, a =>
                {
                    if (removeCounter % 3 == 0 && a.ParentRouteNode != null)
                    {
                        a.ParentRouteNode.ChildNodes.Remove(a);
                    }

                    removeCounter++;
                });

                _action.SaveOrUpdateAction(uow, updatedTree);

                var result = uow.ActionRepository.GetByKey(tree.Id);
                Compare(updatedTree, result, (r, a) =>
                {
                    if (r.Id != a.Id)
                    {
                        throw new Exception("Update failed");
                    }
                });
            }
        }

        [Test]
        public void ActionWithNestedUpdated_AddElements()
        {
            var tree = FixtureData.CreateTestActionTreeWithOnlyActionDo();
            var updatedTree = FixtureData.CreateTestActionTreeWithOnlyActionDo();


            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                Visit(tree, x => uow.ActionRepository.Add(x));

                int addCounter = 0;

                Visit(updatedTree, a =>
                {
                    if (addCounter % 3 == 0 && a.ParentRouteNode != null)
                    {
                        var newAction = new ActionDO
                        {
                            Id = addCounter + 666,
                            ParentRouteNode = a,
                            Name = "____New " + addCounter
                        };

                        a.ParentRouteNode.ChildNodes.Add(newAction);
                        uow.ActionRepository.Add(newAction);
                    }

                    addCounter++;
                });

                for (int i = 0; i < 4; i++)
                {
                    Visit(updatedTree, a =>
                    {
                        if (a.Id > 666)
                        {
                            var newAction = new ActionDO
                            {
                                Id = addCounter + 666,
                                ParentRouteNode = a,
                                Name = "____New " + addCounter
                            };

                            a.ParentRouteNode.ChildNodes.Add(newAction);
                            uow.ActionRepository.Add(newAction);
                        }

                        addCounter++;
                    });
                }

                _action.SaveOrUpdateAction(uow, updatedTree);

                var result = uow.ActionRepository.GetByKey(tree.Id);
                Compare(updatedTree, result, (r, a) =>
                {
                    if (r.Id != a.Id)
                    {
                        throw new Exception("Update failed");
                    }
                });
            }
        }


        [Test]
        public void CreateNewAction()
        {
            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                var plugin = new PluginDO()
                {
                    PluginStatus = PluginStatus.Active,
                    Endpoint = "ep",
                    Version = "1",
                    Name = "plugin",
                };

                uow.PluginRepository.Add(plugin);
                uow.SaveChanges();

                var template = new ActivityTemplateDO("Template1", "label", "1", plugin.Id);
                uow.ActivityTemplateRepository.Add(template);
                var parent = new ActionDO();
                uow.ActionRepository.Add(parent);

                uow.SaveChanges();

                const string actionName = "TestAction";
                var response = _action.Create(uow, template.Id, actionName, null, parent);

                Assert.AreEqual(parent.ChildNodes.Count, 1);
                Assert.AreEqual(parent.ChildNodes[0], response);
                Assert.AreEqual(response.Name, actionName);
            }
        }

        private void Compare(ActionDO reference, ActionDO actual, Action<ActionDO, ActionDO> callback)
        {
            callback(reference, actual);

            if (reference.ChildNodes.Count != actual.ChildNodes.Count)
            {
                throw new Exception("Unable to compare nodes with different number of children.");
            }

            for (int i = 0; i < reference.ChildNodes.Count; i++)
            {
                Compare((ActionDO)reference.ChildNodes[i], (ActionDO)actual.ChildNodes[i], callback);
            }
        }

        private void Visit(ActionDO action, Action<ActionDO> callback)
        {
            callback(action);

            foreach (var child in action.ChildNodes.OfType<ActionDO>().ToArray())
            {
                Visit(child, callback);
            }
        }

        //[Test,Ignore("plugin transmitter in v2 doesn't allow anything except ActioDTO as input param")]
        //public async void CanProcessDocuSignTemplate()
        //{
            // Test.
//            Action action = new Action();
//            var route = FixtureData.TestRoute2();
//            var payloadMappings = FixtureData.FieldMappings;
//            var actionDo = FixtureData.IntegrationTestAction();
//            actionDo.ActivityTemplate.Plugin.Endpoint = "localhost:53234";
//            ProcessDO procesDO = FixtureData.TestProcess1();
//            PluginTransmitterMock
//                .Setup(m => m.CallActionAsync<ActionDataPackageDTO, ActionDTO>(
//                    It.Is<string>(s => s == "testaction"),
//                    It.IsAny<ActionDataPackageDTO>()))
//                .Returns(() => Task.FromResult(Mapper.Map<ActionDTO>(actionDo)))
//                .Verifiable();
//
//            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
//            {
//                uow.RouteRepository.Add(route);
//                uow.ActionRepository.Add(actionDo);
//                uow.ActionListRepository.Add((ActionListDO)actionDo.ParentActivity);
//                uow.ProcessRepository.Add(((ActionListDO)actionDo.ParentActivity).Process);
//                uow.SaveChanges();
//
//                await action.PrepareToExecute(actionDo, procesDO, uow);
//            }
//
//            //Ensure that no Incidents were registered
//            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
//            {
//                Assert.IsFalse(uow.IncidentRepository.GetAll().Any(i => i.PrimaryCategory == "Envelope"));
//            }
//
//            //We use a mock of IPluginTransmitter. Get that mock and check that 
//            //CallActionAsync was called with the correct attributes
//            // TODO: Fix this line according to v2 changes
//            PluginTransmitterMock.Verify();
        //}

        // DO-1270
//        [Test]
//        public void Process_ActionNotUnstarted_ThrowException()
//        {
//            ActionDO actionDo = FixtureData.TestAction9();
//            Action _action = ObjectFactory.GetInstance<Action>();
//            ProcessDO procesDo = FixtureData.TestContainer1();
//
//            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
//            {
//                Assert.AreEqual("Action ID: 2 status is 4.", _action.PrepareToExecute(actionDo, procesDo, uow).Exception.InnerException.Message);
//            }
//        }

//        [Test, Ignore("Ignored execution related tests. Refactoring is going on")]
//        public void Process_ReturnJSONDispatchError_ActionStateError()
//        {
//            ActionDO actionDO = FixtureData.IntegrationTestAction();
//            ProcessDO procesDo = FixtureData.TestProcess1();
//            var pluginClientMock = new Mock<IPluginTransmitter>();
//            pluginClientMock.Setup(s => s.CallActionAsync<ActionDTO>(It.IsAny<string>(), It.IsAny<ActionDTO>())).ThrowsAsync(new RestfulServiceException());
//            ObjectFactory.Configure(cfg => cfg.For<IPluginTransmitter>().Use(pluginClientMock.Object));
//            //_action = ObjectFactory.GetInstance<IAction>();
//
//            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
//            {
//                _action.PrepareToExecute(actionDO, procesDo, uow);
//            }
//
//            Assert.AreEqual(ActionState.Error, actionDO.ActionState);
//        }

//        [Test]
//        public void Process_ReturnJSONDispatchNotError_ActionStateCompleted()
//        {
//            ActionDO actionDO = FixtureData.IntegrationTestAction();
//            actionDO.ActivityTemplate.Plugin.Endpoint = "http://localhost:53234/actions/configure";
//
//            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
//            {
//                uow.ActivityTemplateRepository.Add(actionDO.ActivityTemplate);
//                uow.ActionRepository.Add(actionDO);
//                uow.SaveChanges();
//            }
//
//            ProcessDO procesDO = FixtureData.TestProcess1();
//            var pluginClientMock = new Mock<IPluginTransmitter>();
//            pluginClientMock.Setup(s => s.CallActionAsync<ActionDTO>(It.IsAny<string>(), It.IsAny<ActionDTO>())).Returns<string, ActionDTO>((s, a) => Task.FromResult(a));
//            ObjectFactory.Configure(cfg => cfg.For<IPluginTransmitter>().Use(pluginClientMock.Object));
//            //_action = ObjectFactory.GetInstance<IAction>();
//            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
//            {
//
//                _action.PrepareToExecute(actionDO, procesDO, uow);
//            }
//
//            Assert.AreEqual(ActionState.Active, actionDO.ActionState);
//        }

        [Test]
        public void Process_ActionUnstarted_ShouldBeCompleted()
        {
            //Arrange
            ActionDO actionDo = FixtureData.TestActionUnstarted();
            actionDo.ActivityTemplate.Plugin.Endpoint = "http://localhost:53234/actions/configure";
            actionDo.CrateStorage = JsonConvert.SerializeObject(new ActionDTO());

            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                uow.ActivityTemplateRepository.Add(actionDo.ActivityTemplate);
                uow.ActionRepository.Add(actionDo);
                uow.SaveChanges();
            }

            ActionDTO actionDto = Mapper.Map<ActionDTO>(actionDo);
            PluginTransmitterMock.Setup(rc => rc.PostAsync(It.IsAny<Uri>(), It.IsAny<object>()))
                .Returns(() => Task.FromResult<string>(JsonConvert.SerializeObject(actionDto)));

            ContainerDO containerDO = FixtureData.TestContainer1();

            //Act
            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                var response = _action.PrepareToExecute(actionDo, containerDO, uow);

                //Assert
                Assert.That(response.Status, Is.EqualTo(TaskStatus.RanToCompletion));
            }
        }

        [Test]
        public void AddCrate_AddCratesDTO_UpdatesActionCratesStorage()
        {
            ActionDO actionDO = FixtureData.TestAction23();

            _crate.AddCrate(actionDO, FixtureData.CrateStorageDTO().CrateDTO);

            Assert.IsNotEmpty(actionDO.CrateStorage);
        }

// DO-1270
//        [Test]
//        [ExpectedException(ExpectedMessage = "Action ID: 2 status is 4.")]
//        public async void ActionStateActive_ThrowsException()
//        {
//            ActionDO actionDo = FixtureData.TestActionStateActive();
//            Action _action = ObjectFactory.GetInstance<Action>();
//            ProcessDO procesDo = FixtureData.TestProcess1();
//
//            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
//            {
//                await _action.PrepareToExecute(actionDo, procesDo, uow);
//            }
//        }
//
//        [Test]
//        [ExpectedException(ExpectedMessage = "Action ID: 2 status is 4.")]
//        public async void ActionStateDeactive_ThrowsException()
//        {
//            ActionDO actionDo = FixtureData.TestActionStateDeactive();
//            Action _action = ObjectFactory.GetInstance<Action>();
//            ProcessDO procesDo = FixtureData.TestProcess1();
//
//            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
//            {
//                await _action.PrepareToExecute(actionDo, procesDo, uow);
//            }
//        }

//        [Test]
//        [ExpectedException(ExpectedMessage = "Action ID: 2 status is 4.")]
//        public async void ActionStateError_ThrowsException()
//        {
//            ActionDO actionDo = FixtureData.TestActionStateError();
//            Action _action = ObjectFactory.GetInstance<Action>();
//            ProcessDO procesDo = FixtureData.TestProcess1();
//
//            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
//            {
//                await _action.PrepareToExecute(actionDo, procesDo, uow);
//            }
//        }

        [Test]
        public async void PrepareToExecute_WithMockedExecute_WithoutPayload()
        {
            ActionDO actionDo = FixtureData.TestActionStateInProcess();
            actionDo.CrateStorage = JsonConvert.SerializeObject(new ActionDTO());

            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                uow.ActivityTemplateRepository.Add(actionDo.ActivityTemplate);
                uow.ActionRepository.Add(actionDo);
                uow.SaveChanges();
            }

            Action _action = ObjectFactory.GetInstance<Action>();
            ContainerDO containerDO = FixtureData.TestContainer1();
            EventManager.EventActionStarted += EventManager_EventActionStarted;
            var executeActionMock = new Mock<IAction>();
            executeActionMock.Setup(s => s.Run(actionDo, containerDO)).Returns<Task<PayloadDTO>>(null);

            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                var count = uow.ActionRepository.GetAll().Count();
                await _action.PrepareToExecute(actionDo, containerDO, uow);
                //Assert.AreEqual(uow.ActionRepository.GetAll().Count(), count + 1);
            }
            Assert.IsNull(containerDO.CrateStorage);
            Assert.IsTrue(_eventReceived);
           // Assert.AreEqual(actionDo.ActionState, ActionState.Active);
        }

        [Test]
        public async void PrepareToExecute_WithMockedExecute_WithPayload()
        {
            ActionDO actionDo = FixtureData.TestActionStateInProcess();
            actionDo.CrateStorage = JsonConvert.SerializeObject(new ActionDTO() { Label = "Test Action" });

            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                uow.ActivityTemplateRepository.Add(actionDo.ActivityTemplate);
                uow.ActionRepository.Add(actionDo);
                uow.SaveChanges();
            }

            IAction _action = ObjectFactory.GetInstance<IAction>();
            ContainerDO containerDO = FixtureData.TestContainer1();
            EventManager.EventActionStarted += EventManager_EventActionStarted;

            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                var pluginClientMock = new Mock<IPluginTransmitter>();
                pluginClientMock.Setup(s => s.CallActionAsync<PayloadDTO>(It.IsAny<string>(), It.IsAny<ActionDTO>()))
                                .Returns(Task.FromResult(new PayloadDTO(actionDo.CrateStorage, containerDO.Id)));
                ObjectFactory.Configure(cfg => cfg.For<IPluginTransmitter>().Use(pluginClientMock.Object));

                var count = uow.ActionRepository.GetAll().Count();
                await _action.PrepareToExecute(actionDo, containerDO, uow);
                //Assert.AreEqual(uow.ActionRepository.GetAll().Count(), count + 1);
            }
            Assert.IsNotNull(containerDO.CrateStorage);
            Assert.IsTrue(_eventReceived);
           // Assert.AreEqual(actionDo.ActionState, ActionState.Active);
        }

        [Test]
        public async void ActionStarted_EventRaisedSuccessfully()
        {
            ActionDO actionDo = FixtureData.TestActionStateInProcess();
            actionDo.CrateStorage = JsonConvert.SerializeObject(new ActionDTO());

            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                uow.ActivityTemplateRepository.Add(actionDo.ActivityTemplate);
                uow.ActionRepository.Add(actionDo);
                uow.SaveChanges();
            }


            Action _action = ObjectFactory.GetInstance<Action>();
            ContainerDO containerDO = FixtureData.TestContainer1();
            EventManager.EventActionStarted += EventManager_EventActionStarted;
            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                var count = uow.ActionRepository.GetAll().Count();
                await _action.PrepareToExecute(actionDo, containerDO, uow);
                //Assert.AreEqual(uow.ActionRepository.GetAll().Count(), count + 1);
            }
            Assert.IsTrue(_eventReceived);
        //            Assert.AreEqual(actionDo.ActionState, ActionState.Active);
        }

        private void EventManager_EventActionStarted(ActionDO action)
        {
            _eventReceived = true;
        }

        // DO-1214
//        private void UpdateDatabase(ActionDO curActionDo)
//        {
//
//            curActionDo.ActivityTemplate.Plugin.Endpoint = "pluginDocusign";
//            _uow.ActivityTemplateRepository.Add(curActionDo.ActivityTemplate);
//            _uow.SaveChanges();
//
//            _uow.RouteRepository.Add(FixtureData.TestRoute1());
//
//            ActionListDO parentActivity = (ActionListDO)curActionDo.ParentActivity;
//            parentActivity.Process.RouteId = 33;
//            _uow.ProcessRepository.Add(parentActivity.Process);
//            _uow.SaveChanges();
//
//            _uow.ActionListRepository.Add(parentActivity);
//            _uow.SaveChanges();
//
//            _uow.ActionRepository.Add(curActionDo);
//            _uow.SaveChanges();
//        }
    }

    internal class TestActionService : Action
    {
        private IPluginTransmitter _restfulServiceClient;

        internal IPluginTransmitter RestfulServiceClient
        {
            get
            {
                if (_restfulServiceClient == null)
                {
                    _restfulServiceClient = new Mock<IPluginTransmitter>(MockBehavior.Default).Object;
                }

                return _restfulServiceClient;
            }
            private set { _restfulServiceClient = value; }
        }
    }
}
