﻿using Data.Infrastructure.AutoMapper;
using Hub.StructureMap;
using Microsoft.Owin;
using Owin;
using TerminalBase.BaseClasses;
using terminalGoogle;
using TerminalBase.Infrastructure;
using DependencyType = Hub.StructureMap.StructureMapBootStrapper.DependencyType;
using System;
using System.Web.Http.Dispatcher;
using System.Collections.Generic;

[assembly: OwinStartup(typeof(terminalGoogle.Startup))]

namespace terminalGoogle
{
    public class Startup : BaseConfiguration
    {
        public void Configuration(IAppBuilder app)
        {
            Configuration(app, false);
        }

        public void Configuration(IAppBuilder app, bool selfHost)
        {
            ConfigureProject(selfHost, TerminalGoogleBootstrapper.ConfigureLive);
            RoutesConfig.Register(_configuration);
            ConfigureFormatters();
            app.UseWebApi(_configuration);

            if (!selfHost)
            {
                StartHosting("terminalGoogle");
            }
        }

        public override ICollection<Type> GetControllerTypes(IAssembliesResolver assembliesResolver)
        {
            return new Type[] {
                    typeof(Controllers.ActivityController),
                    typeof(Controllers.EventController),
                    typeof(Controllers.AuthenticationController),
                    typeof(Controllers.TerminalController)
                };
        }
    }
}