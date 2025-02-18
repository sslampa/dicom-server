﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;

namespace Microsoft.Health.Dicom.Web.Tests.E2E.Common;

public class TestDicomWebServerFactory
{
    public static TestDicomWebServer GetTestDicomWebServer(Type startupType, bool enableDataPartitions = false)
    {
        string environmentUrl = GetEnvironmentUrl(enableDataPartitions);

        if (string.IsNullOrEmpty(environmentUrl))
        {
            return new InProcTestDicomWebServer(startupType, enableDataPartitions);
        }

        if (environmentUrl[^1] != '/')
        {
            environmentUrl += "/";
        }

        return new RemoteTestDicomWebServer(new Uri(environmentUrl));
    }

    private static string GetEnvironmentUrl(bool enableDataPartitions = false)
    {
        return enableDataPartitions ? Environment.GetEnvironmentVariable("TestFeaturesEnabledEnvironmentUrl") : Environment.GetEnvironmentVariable("TestEnvironmentUrl");
    }
}
