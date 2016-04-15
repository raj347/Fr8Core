﻿using System.Linq;
using Hub.Services;
using Data.Interfaces;
using NUnit.Framework;
using StructureMap;
using UtilitiesTesting;
using UtilitiesTesting.Fixtures;

namespace DockyardTest.Services
{
    [TestFixture]
    [Category("ContainerService")]
    public class ContainerServiceTests : BaseTest
    {
        private Fr8Account _userService;
        private string _testUserId = "testuser";

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            _userService = ObjectFactory.GetInstance<Fr8Account>();
        }

        [Test]
        public void ContainerService_CanRetrieveValidContainers()
        {
            //Arrange 
            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                var plan = FixtureData.TestPlan5();
                uow.UserRepository.Add(plan.Fr8Account);
                uow.PlanRepository.Add(plan);
                foreach (var container in FixtureData.GetContainers())
                {
                    uow.ContainerRepository.Add(container);
                }
                uow.SaveChanges();
            }

            //Act
            var containerList = _userService.GetContainerList(_testUserId);

            //Assert
            Assert.AreEqual(2, containerList.Count());
        }
    }
}