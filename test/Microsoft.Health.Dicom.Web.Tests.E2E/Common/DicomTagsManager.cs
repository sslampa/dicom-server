﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EnsureThat;
using Microsoft.Health.Dicom.Client;
using Microsoft.Health.Dicom.Client.Models;
using Microsoft.Health.Operations;
using Polly;
using Polly.Retry;
using Xunit;

namespace Microsoft.Health.Dicom.Web.Tests.E2E.Common;

internal class DicomTagsManager : IAsyncDisposable
{
    private readonly IDicomWebClient _dicomWebClient;
    private readonly HashSet<string> _tags;

    private static readonly AsyncRetryPolicy<OperationState<DicomOperation>> GetOperationStateRetryPolicy = Policy
       .HandleResult<OperationState<DicomOperation>>(x => x.Status.IsInProgress())
       .WaitAndRetryAsync(100, x => TimeSpan.FromSeconds(3)); // Retry 100 times and wait for 3 seconds after each retry

    public DicomTagsManager(IDicomWebClient dicomWebClient)
    {
        _dicomWebClient = EnsureArg.IsNotNull(dicomWebClient, nameof(dicomWebClient));
        _tags = new HashSet<string>();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var tag in _tags)
        {
            await _dicomWebClient.DeleteExtendedQueryTagAsync(tag);
        }
    }

    public Task<OperationStatus> AddTagsAsync(params AddExtendedQueryTagEntry[] entries)
        => AddTagsAsync(entries, CancellationToken.None);

    public async Task<OperationStatus> AddTagsAsync(IEnumerable<AddExtendedQueryTagEntry> entries, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(entries, nameof(entries));
        foreach (AddExtendedQueryTagEntry entry in entries)
        {
            _tags.Add(entry.Path);
        }

        DicomWebResponse<DicomOperationReference> response = await _dicomWebClient.AddExtendedQueryTagAsync(entries, cancellationToken);
        DicomOperationReference operation = await response.GetValueAsync();

        OperationState<DicomOperation> result = await GetOperationStateRetryPolicy.ExecuteAsync(async () =>
        {
            var operationStatus = await _dicomWebClient.GetOperationStateAsync(operation.Id);
            return await operationStatus.GetValueAsync();
        });

        // Check reference
        DicomWebResponse<OperationState<DicomOperation>> actualResponse = await _dicomWebClient.ResolveReferenceAsync(operation, cancellationToken);
        OperationState<DicomOperation> actual = await actualResponse.GetValueAsync();
        Assert.Equal(result.OperationId, actual.OperationId);
        Assert.Equal(result.Status, actual.Status);

        return result.Status;
    }

    public async Task<GetExtendedQueryTagEntry> GetTagAsync(string tagPath, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(tagPath, nameof(tagPath));

        var response = await _dicomWebClient.GetExtendedQueryTagAsync(tagPath, cancellationToken);
        return await response.GetValueAsync();
    }

    public async Task<IReadOnlyList<GetExtendedQueryTagEntry>> GetTagsAsync(int limit, int offset, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsGte(limit, 1, nameof(limit));
        EnsureArg.IsGte(offset, 0, nameof(offset));

        var response = await _dicomWebClient.GetExtendedQueryTagsAsync(limit, offset, cancellationToken);
        return await response.GetValueAsync();
    }

    public async Task<IReadOnlyList<ExtendedQueryTagError>> GetTagErrorsAsync(string tagPath, int limit, int offset, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(tagPath, nameof(tagPath));
        EnsureArg.IsGte(limit, 1, nameof(limit));
        EnsureArg.IsGte(offset, 0, nameof(offset));

        var response = await _dicomWebClient.GetExtendedQueryTagErrorsAsync(tagPath, limit, offset, cancellationToken);
        return await response.GetValueAsync();
    }

    public async Task<GetExtendedQueryTagEntry> UpdateExtendedQueryTagAsync(string tagPath, UpdateExtendedQueryTagEntry newValue, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(tagPath, nameof(tagPath));

        var response = await _dicomWebClient.UpdateExtendedQueryTagAsync(tagPath, newValue, cancellationToken);
        return await response.GetValueAsync();
    }

    public async Task<bool> DeleteExtendedQueryTagAsync(string tagPath, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(tagPath, nameof(tagPath));
        _tags.Remove(tagPath);

        try
        {
            var response = await _dicomWebClient.DeleteExtendedQueryTagAsync(tagPath, cancellationToken);
            return response.StatusCode == System.Net.HttpStatusCode.NoContent;
        }
        catch (DicomWebException dwe) when (dwe.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}
