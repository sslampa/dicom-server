﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

namespace Microsoft.Health.Dicom.Core.Exceptions;

/// <summary>
/// Exception thrown when the Workitem is in the final state.
/// </summary>
public class WorkitemIsInFinalStateException : DicomServerException
{
    public WorkitemIsInFinalStateException(string workitemUid, string procedureStepState)
        : base(string.Format(DicomCoreResource.WorkitemIsInFinalState, workitemUid, procedureStepState))
    {
    }
}
