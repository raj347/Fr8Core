﻿using System.Threading.Tasks;

namespace Hub.Interfaces
{
    public interface IManifestRegistryMonitor
    {
        Task StartMonitoringManifestRegistrySubmissions();
    }
}
