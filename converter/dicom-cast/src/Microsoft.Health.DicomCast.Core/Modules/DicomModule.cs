﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using EnsureThat;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Health.Dicom.Client;
using Microsoft.Health.DicomCast.Core.Configurations;
using Microsoft.Health.DicomCast.Core.Extensions;
using Microsoft.Health.DicomCast.Core.Features.DicomWeb.Service;
using Microsoft.Health.Extensions.DependencyInjection;

namespace Microsoft.Health.DicomCast.Core.Modules;

public class DicomModule : IStartupModule
{
    private const string DicomWebConfigurationSectionName = "DicomWeb";

    private readonly IConfiguration _configuration;

    public DicomModule(IConfiguration configuration)
    {
        EnsureArg.IsNotNull(configuration, nameof(configuration));

        _configuration = configuration;
    }

    public void Load(IServiceCollection services)
    {
        EnsureArg.IsNotNull(services, nameof(services));

        var dicomWebConfiguration = new DicomWebConfiguration();
        IConfigurationSection dicomWebConfigurationSection = _configuration.GetSection(DicomWebConfigurationSectionName);
        dicomWebConfigurationSection.Bind(dicomWebConfiguration);

        services.AddSingleton(Options.Create(dicomWebConfiguration));

        services.AddHttpClient<IDicomWebClient, DicomWebClient>(sp =>
            {
                sp.BaseAddress = dicomWebConfiguration.Endpoint;
            })
            .AddAuthenticationHandler(services, dicomWebConfigurationSection.GetSection(AuthenticationConfiguration.SectionName), DicomWebConfigurationSectionName);

        services.Add<ChangeFeedRetrieveService>()
            .Singleton()
            .AsSelf()
            .AsImplementedInterfaces();
    }
}
