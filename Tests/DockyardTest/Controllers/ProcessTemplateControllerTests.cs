﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Security.Principal;
using System.Web.Http.Results;
using Core.Interfaces;
using Data.Entities;
using Data.Interfaces;
using NUnit.Framework;
using StructureMap;
using StructureMap.AutoMocking;
using UtilitiesTesting;
using UtilitiesTesting.Fixtures;
using Web.Controllers;
using Web.ViewModels;

namespace DockyardTest.Controllers
{
    [TestFixture]
    [Category("Controllers.Api.ProcessTemplateService")]
    public class ProcessTemplateControllerTests : BaseTest
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
        }

        [Test]
        public void ProcessTemplateController_CanAddNewProcessTemplate()
        {
            //Arrange 
            string testUserId = "testuser";
            var processTemplateDto = FixtureData.TestProcessTemplateDTO();


            //Act
            ProcessTemplateController ptc = CreateProcessTemplateController(testUserId);
            var response = ptc.Post(processTemplateDto);

            //Assert
            var okResult = response as OkNegotiatedContentResult<ProcessTemplateDTO>;
            Assert.NotNull(okResult);
            using (var uow = ObjectFactory.GetInstance<IUnitOfWork>())
            {
                Assert.AreEqual(0, ptc.ModelState.Count()); //must be no errors
                var ptdo = uow.ProcessTemplateRepository.
                    GetQuery().SingleOrDefault(pt => pt.UserId == testUserId && pt.Name == processTemplateDto.Name);
                Assert.IsNotNull(ptdo);
                Assert.AreEqual(processTemplateDto.Description, ptdo.Description);
            }
        }

        [Test]
        public void ProcessTemplateController_Will_Return_BadResult_If_Name_Is_Empty()
        {
            //Arrange 
            string testUserId = "testuser";
            var processTemplateDto = FixtureData.TestProcessTemplateDTO();
            processTemplateDto.Name = String.Empty;


            //Act
            ProcessTemplateController ptc = CreateProcessTemplateController(testUserId);
            var response = ptc.Post(processTemplateDto);

            //Assert
            var badResult = response as BadRequestErrorMessageResult;
            Assert.NotNull(badResult);

        }

        [Test]
        public void ProcessTemplateController_Will_ThrowException_If_No_ProcessTemplate_Found()
        {
            //Arrange 
            string testUserId = "testuser";



            //Act
            ProcessTemplateController processTemplateController = CreateProcessTemplateController(testUserId);


            //Assert

            Assert.Throws<ApplicationException>(() =>
            {
                processTemplateController.Get(55);

            }, "Process Template not found for id 55");

        }

        [Test]
        public void ProcessController_Will_Return_All_When_Get_Invoked_With_Null()
        {
            //Arrange
            var testUserId = "testuser1";

            var processTemplateController = CreateProcessTemplateController(testUserId);


            for (var i = 0; i < 4; i++)
            {
                var processTemplateDto = FixtureData.TestProcessTemplateDTO();
                processTemplateController.Post(processTemplateDto);

            }

            //Act
            var actionResult = processTemplateController.Get() as OkNegotiatedContentResult<IEnumerable<ProcessTemplateDTO>>;

            //Assert
            Assert.NotNull(actionResult);
            Assert.AreEqual(4, actionResult.Content.Count());

        }

        [Test]
        public void ProcessController_Will_Return_One_When_Get_Invoked_With_Id()
        {
            //Arrange
            var testUserId = "testuser4";
            var processTemplateController = CreateProcessTemplateController(testUserId);
            var processTemplateDto = FixtureData.TestProcessTemplateDTO();
            processTemplateController.Post(processTemplateDto);



            //Act
            var actionResult = processTemplateController.Get(processTemplateDto.Id) as OkNegotiatedContentResult<ProcessTemplateDTO>;

            //Assert
            Assert.NotNull(actionResult);
            Assert.NotNull(actionResult.Content);
            Assert.AreEqual(processTemplateDto.Id, actionResult.Content.Id);

        }

        [Test]
        public void ProcessTemplateController_CanDelete()
        {
            //Arrange 
            string testUserId = "testuser3";
            var processTemplateDto = FixtureData.TestProcessTemplateDTO();



            ProcessTemplateController processTemplateController = CreateProcessTemplateController(testUserId);
            var postResult = processTemplateController.Post(processTemplateDto) as OkNegotiatedContentResult<ProcessTemplateDTO>;

            Assert.NotNull(postResult);

            //Act
            var deleteResult = processTemplateController.Delete(postResult.Content.Id) as OkResult;

            Assert.NotNull(deleteResult);

            //Assert
            Assert.Throws<ApplicationException>(() =>
            {
                processTemplateController.Get(postResult.Content.Id);

            }, "Process Template not found for id " + postResult.Content.Id);
        }


        [Test]
        public void ProcessController_CannotCreateIfProcessNameIsEmpty()
        {
            //Arrange 
            string testUserId = "testuser";
            var processTemplateDto = FixtureData.TestProcessTemplateDTO();
            processTemplateDto.Name = String.Empty;

            //Act
            ProcessTemplateController processTemplateController = CreateProcessTemplateController(testUserId);
            processTemplateController.Post(processTemplateDto);

            //Assert
            Assert.AreEqual(1, processTemplateController.ModelState.Count()); //must be one error
        }

        [Test]
        public void ProcessController_CanEditProcess()
        {
            //Arrange 
            string testUserId = "testuser2";
            var processTemplateDto = FixtureData.TestProcessTemplateDTO();
            var processTemplateController = CreateProcessTemplateController(testUserId);
            
            //Save First
            var postResult = processTemplateController.Post(processTemplateDto) as OkNegotiatedContentResult<ProcessTemplateDTO>;
            Assert.NotNull(postResult);

            //Then Get
            var getResult = processTemplateController.Get(postResult.Content.Id) as OkNegotiatedContentResult<ProcessTemplateDTO>;
            Assert.NotNull(getResult);

            //Then Edit
            var postEditNameValue = "EditedName";
            getResult.Content.Name = postEditNameValue;
            var editResult = processTemplateController.Post(getResult.Content) as OkNegotiatedContentResult<ProcessTemplateDTO>;
            Assert.NotNull(editResult);

            //Then Get
            var postEditGetResult = processTemplateController.Get(editResult.Content.Id) as OkNegotiatedContentResult<ProcessTemplateDTO>;
            Assert.NotNull(postEditGetResult);

            //Assert 
            Assert.AreEqual(postEditGetResult.Content.Name,postEditNameValue);
            Assert.AreEqual(postEditGetResult.Content.Id,editResult.Content.Id);
            Assert.AreEqual(postEditGetResult.Content.Id, postResult.Content.Id);
            Assert.AreEqual(postEditGetResult.Content.Id, getResult.Content.Id);
            
        }



       

        private static ProcessTemplateController CreateProcessTemplateController(string testUserId)
        {

            var ptc = new ProcessTemplateController
            {
                User = new GenericPrincipal(new GenericIdentity(testUserId, "Forms"), new[] { "USers" })
            };
            return ptc;
        }
    }
}
