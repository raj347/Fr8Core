﻿using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using HealthMonitor.Utility;
using Fr8Data.Control;
using Fr8Data.Crates;
using Fr8Data.DataTransferObjects;
using Fr8Data.Manifests;
using terminalAtlassianTests.Fixtures;

namespace terminalAtlassianTests.Integration
{
    [Explicit]
    public class Get_Jira_Issue_v1_Tests : BaseTerminalIntegrationTest
    {
        public override string TerminalName
        {
            get { return "terminalAtlassian"; }
        }

        [Test]
        public async Task Configure_Initial()
        {
            await ConfigureInitial();
        }

        [Test]
        public async Task Configure_FollowUp()
        {
            await ConfigureFollowUp();
        }

        [Test]
        public async Task Run_CheckPayloadDTO()
        {
            var activityDTO = await ConfigureFollowUp();
            activityDTO.AuthToken = HealthMonitor_FixtureData.Jira_AuthToken();

            var runUrl = GetTerminalRunUrl();
            var data = new Fr8DataDTO()
            {
                ActivityDTO = activityDTO
            };

            AddPayloadCrate(data, new OperationalStateCM());

            var payload = await HttpPostAsync<Fr8DataDTO, PayloadDTO>(runUrl, data);
            Assert.IsNotNull(payload);
            Assert.IsNotNull(payload.CrateStorage);

            var crateStorage = Crate.FromDto(payload.CrateStorage);
            Assert.AreEqual(2, crateStorage.Count);
            Assert.AreEqual(1, crateStorage.CratesOfType<OperationalStateCM>().Count());
            Assert.AreEqual(1, crateStorage.CratesOfType<StandardPayloadDataCM>().Count());

            var payloadData = crateStorage.CrateContentsOfType<StandardPayloadDataCM>().First();
            Assert.IsNotNull(payloadData.PayloadObjects);
            Assert.AreEqual(1, payloadData.PayloadObjects.Count);
            Assert.AreEqual("FR-1245", payloadData.PayloadObjects[0].GetValue("Key"));
        }

        private async Task<ActivityDTO> ConfigureInitial()
        {
            var configureUrl = GetTerminalConfigureUrl();

            var dataDTO = HealthMonitor_FixtureData
                .Get_Jira_Issue_v1_InitialConfiguration_Fr8DataDTO();

            var activityDTO =
                await HttpPostAsync<Fr8DataDTO, ActivityDTO>(
                    configureUrl,
                    dataDTO
                );

            Assert.IsNotNull(activityDTO);
            Assert.IsNotNull(activityDTO.CrateStorage);

            var crateStorage = Crate.FromDto(activityDTO.CrateStorage);
            Assert.AreEqual(1, crateStorage.Count);
            Assert.AreEqual(1, crateStorage.CratesOfType<StandardConfigurationControlsCM>().Count());

            var controls = crateStorage.CrateContentsOfType<StandardConfigurationControlsCM>().First();
            Assert.AreEqual(1, controls.Controls.Count);
            Assert.AreEqual("TextSource", controls.Controls[0].Type);
            Assert.AreEqual("IssueNumber", controls.Controls[0].Name);

            return activityDTO;
        }

        private async Task<ActivityDTO> ConfigureFollowUp()
        {
            var activityDTO = await ConfigureInitial();
            activityDTO.AuthToken = HealthMonitor_FixtureData.Jira_AuthToken();

            using (var updater = Crate.UpdateStorage(() => activityDTO.CrateStorage))
            {
                var controls = updater.CrateContentsOfType<StandardConfigurationControlsCM>().First();
                var issueNumber = controls.FindByName<TextSource>("IssueNumber");
                issueNumber.TextValue = "FR-1245";
                issueNumber.ValueSource = "specific";
            }

            var configureUrl = GetTerminalConfigureUrl();
            activityDTO =
                await HttpPostAsync<Fr8DataDTO, ActivityDTO>(
                    configureUrl,
                    new Fr8DataDTO() { ActivityDTO = activityDTO }
                );

            Assert.IsNotNull(activityDTO);
            Assert.IsNotNull(activityDTO.CrateStorage);

            var crateStorage = Crate.FromDto(activityDTO.CrateStorage);
            Assert.AreEqual(2, crateStorage.Count);
            Assert.AreEqual(1, crateStorage.CratesOfType<StandardConfigurationControlsCM>().Count());
            Assert.AreEqual(1, crateStorage.CratesOfType<FieldDescriptionsCM>().Count());

            var fieldDescriptions = crateStorage.CrateContentsOfType<FieldDescriptionsCM>().First();
            Assert.True(fieldDescriptions.Fields.Any(x => x.Key == "Key" && x.Value == "FR-1245"));

            return activityDTO;
        }
    }
}
