﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Facebook;
using Fr8.Infrastructure.Data.DataTransferObjects;
using Fr8.Infrastructure.Interfaces;
using Fr8.Infrastructure.Utilities.Configuration;
using Fr8.TerminalBase.Errors;
using Newtonsoft.Json.Linq;
using terminalFacebook.Interfaces;

namespace terminalFacebook.Services
{
    public class FacebookIntegration : IFacebookIntegration
    {

        public FacebookIntegration()
        {
        }

        /// <summary>
        /// Build external Slack OAuth url.
        /// </summary>
        public string CreateAuthUrl(string externalStateToken)
        {
            var fb = new FacebookClient();
            var fbOauthLoginUri = fb.GetLoginUrl(new {
                client_id = CloudConfigurationManager.GetSetting("FacebookId"),
                client_secret = CloudConfigurationManager.GetSetting("FacebookSecret"),
                redirect_uri = CloudConfigurationManager.GetSetting("HubOAuthRedirectUri"),
                response_type = "code",
                scope = "email, publish_actions",
                state = externalStateToken
            });

            return fbOauthLoginUri.ToString();
        }

        public async Task<string> GetOAuthToken(string code)
        {
            var fb = new FacebookClient();
            dynamic result = fb.Post("oauth/access_token", new
            {
                client_id = CloudConfigurationManager.GetSetting("FacebookId"),
                client_secret = CloudConfigurationManager.GetSetting("FacebookSecret"),
                redirect_uri = CloudConfigurationManager.GetSetting("HubOAuthRedirectUri"),
                code = code
            });

            return result.access_token;
        }

        public async Task<UserInfo> GetUserInfo(string oauthToken)
        {
            var fb = new FacebookClient(oauthToken);
            dynamic userInfo = fb.Get("me?fields=first_name,last_name,id,email");
            return new UserInfo
            {
                UserId = userInfo.id,
                UserName = userInfo.first_name + " " + userInfo.last_name
            };
        }

        public async Task PostToTimeline(string oauthToken, string message)
        {
            var fb = new FacebookClient(oauthToken);
            var post = new 
            {
                message = message,
                caption = "Caption",
                description = "description",
                name = "hey name"
            };
            await fb.PostTaskAsync("/me/feed", post);
        }
    }
}