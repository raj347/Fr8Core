using System.Collections.Generic;
using System.Web.Http;
using Data.Entities;
using Data.States;
using Utilities.Configuration.Azure;
using System.Web.Http.Description;
using Data.Interfaces.Manifests;

namespace terminalPapertrial.Controllers
{
    [RoutePrefix("terminals")]
    public class TerminalController : ApiController
    {
        [HttpGet]
        [Route("discover")]
        [ResponseType(typeof(StandardFr8TerminalCM))]
        public IHttpActionResult Get()
        {
            var terminal = new TerminalDO()
            {
                Name = "terminalPapertrial",
                TerminalStatus = TerminalStatus.Active,
                Endpoint = CloudConfigurationManager.GetSetting("TerminalEndpoint"),
                Version = "1"
            };

            //var waitForDocusignEventActionTemplate = new ActivityTemplateDO()
            //{
            //    Version = "1",
            //    Name = "Monitor_DocuSign",
            //    Label = "Monitor DocuSign",
            //    Category = ActivityCategory.Monitors,
            //    Terminal = terminal,
            //    AuthenticationType = AuthenticationType.Internal,
            //    MinPaneWidth = 330
            //};

            //var sendDocuSignEnvelopeActionTemplate = new ActivityTemplateDO()
            //{
            //    Version = "1",
            //    Name = "Send_DocuSign_Envelope",
            //    Label = "Send DocuSign Envelope",
            //    Category = ActivityCategory.Forwarders,
            //    Terminal = terminal,
            //    AuthenticationType = AuthenticationType.Internal,
            //    MinPaneWidth = 330
            //};

            //var extractDataFromEnvelopeActionTemplate = new ActivityTemplateDO()
            //{
            //    Version = "1",
            //    Name = "Receive_DocuSign_Envelope",
            //    Label = "Receive DocuSign Envelope",
            //    Category = ActivityCategory.Receivers,
            //    Terminal = terminal,
            //    AuthenticationType = AuthenticationType.Internal,
            //    MinPaneWidth = 330
            //};

            //var recordDocuSignEvents = new ActivityTemplateDO
            //{
            //    Name = "Record_DocuSign_Events",
            //    Label = "Record DocuSign Events",
            //    Version = "1",
            //    Category = ActivityCategory.Forwarders,
            //    Terminal = terminal,
            //    AuthenticationType = AuthenticationType.Internal,
            //    MinPaneWidth = 330
            //};

            //var mailMergeActionTemplate = new ActivityTemplateDO
            //{
            //    Name = "Mail_Merge_Into_DocuSign",
            //    Label = "Mail Merge Into DocuSign",
            //    Version = "1",
            //    AuthenticationType = AuthenticationType.Internal,
            //    Category = ActivityCategory.Solution,
            //    Terminal = terminal,
            //    MinPaneWidth = 500
            //};

            //var collectFormDataSolution = new ActivityTemplateDO
            //{
            //    Name = "Collect_Form_Data_Solution",
            //    Label = "Collect Form Data Solution",
            //    Version = "1",
            //    Category = ActivityCategory.Solution,
            //    Terminal = terminal,
            //    MinPaneWidth = 380
            //};

            var actionList = new List<ActivityTemplateDO>()
            {
                //waitForDocusignEventActionTemplate,
                //extractDataFromEnvelopeActionTemplate,
                //sendDocuSignEnvelopeActionTemplate,
                //recordDocuSignEvents,
                //mailMergeActionTemplate,
                //collectFormDataSolution
            };

            StandardFr8TerminalCM curStandardFr8TerminalCM = new StandardFr8TerminalCM()
            {
                Definition = terminal,
                Actions = actionList
            };
            return Json(curStandardFr8TerminalCM);
        }
    }
}