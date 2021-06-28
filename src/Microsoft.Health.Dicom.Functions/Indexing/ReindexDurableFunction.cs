// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using EnsureThat;
using Microsoft.Extensions.Options;
using Microsoft.Health.Dicom.Core.Configs;
using Microsoft.Health.Dicom.Core.Features.ExtendedQueryTag;
using Microsoft.Health.Dicom.Core.Features.Indexing;
using Microsoft.Health.Dicom.Core.Features.Retrieve;
using Microsoft.Health.Dicom.SqlServer.Features.Schema;
using Microsoft.Health.SqlServer.Features.Schema;

namespace Microsoft.Health.Dicom.Functions.Indexing
{
    /// <summary>
    /// Represents the Azure Durable Functions that perform the re-indexing of previously added DICOM instances
    /// based on new tags configured by the user.
    /// </summary>
    public partial class ReindexDurableFunction
    {
        private readonly ReindexOperationConfiguration _reindexConfig;
        private readonly IReindexStateStore _reindexStore;
        private readonly IInstanceReindexer _instanceReindexer;
        private readonly IAddExtendedQueryTagService _addExtendedQueryTagService;
        private readonly IInstanceStore _instanceStore;
        private readonly IExtendedQueryTagStore _extendedQueryTagStore;
        private readonly ISchemaVersionResolver _schemaVersionResolver;
        private readonly SchemaInformation _schemaInformation;

        public ReindexDurableFunction(
            IOptions<ReindexOperationConfiguration> configOptions,
            IAddExtendedQueryTagService addExtendedQueryTagService,
            IReindexStateStore reindexStore,
            IInstanceStore instanceStore,
            IInstanceReindexer instanceReindexer,
            IExtendedQueryTagStore extendedQueryTagStore,
            ISchemaVersionResolver schemaVersionResolver,
            SchemaInformation schemaInformation)
        {
            EnsureArg.IsNotNull(configOptions, nameof(configOptions));
            EnsureArg.IsNotNull(reindexStore, nameof(reindexStore));
            EnsureArg.IsNotNull(instanceReindexer, nameof(instanceReindexer));
            EnsureArg.IsNotNull(addExtendedQueryTagService, nameof(addExtendedQueryTagService));
            EnsureArg.IsNotNull(instanceStore, nameof(instanceStore));
            EnsureArg.IsNotNull(extendedQueryTagStore, nameof(extendedQueryTagStore));
            EnsureArg.IsNotNull(schemaVersionResolver, nameof(schemaVersionResolver));
            EnsureArg.IsNotNull(schemaInformation, nameof(schemaInformation));
            _reindexConfig = configOptions.Value;
            _reindexStore = reindexStore;
            _instanceReindexer = instanceReindexer;
            _addExtendedQueryTagService = addExtendedQueryTagService;
            _instanceStore = instanceStore;
            _extendedQueryTagStore = extendedQueryTagStore;
            _schemaVersionResolver = schemaVersionResolver;
            _schemaInformation = schemaInformation;
        }
    }
}
