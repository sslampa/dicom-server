﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using EnsureThat;
using FellowOakDicom;
using Microsoft.Extensions.Options;
using Microsoft.Health.Dicom.Core.Configs;
using Microsoft.Health.Dicom.Core.Extensions;
using Microsoft.Health.Dicom.Core.Features.ExtendedQueryTag;
using Microsoft.Health.Dicom.Core.Features.Validation;

namespace Microsoft.Health.Dicom.Core.Features.Store;

/// <summary>
/// Provides functionality to validate a <see cref="DicomDataset"/> to make sure it meets the minimum requirement when storing.
/// </summary>
public class StoreDatasetValidator : IStoreDatasetValidator
{
    private readonly bool _enableFullDicomItemValidation;
    private readonly IElementMinimumValidator _minimumValidator;
    private readonly IQueryTagService _queryTagService;

    public StoreDatasetValidator(IOptions<FeatureConfiguration> featureConfiguration, IElementMinimumValidator minimumValidator, IQueryTagService queryTagService)
    {
        EnsureArg.IsNotNull(featureConfiguration?.Value, nameof(featureConfiguration));
        EnsureArg.IsNotNull(minimumValidator, nameof(minimumValidator));
        EnsureArg.IsNotNull(queryTagService, nameof(queryTagService));

        _enableFullDicomItemValidation = featureConfiguration.Value.EnableFullDicomItemValidation;
        _minimumValidator = minimumValidator;
        _queryTagService = queryTagService;
    }

    /// <inheritdoc/>
    public async Task<ValidationWarnings> ValidateAsync(DicomDataset dicomDataset, string requiredStudyInstanceUid, CancellationToken cancellationToken)
    {
        EnsureArg.IsNotNull(dicomDataset, nameof(dicomDataset));

        ValidateCoreTags(dicomDataset, requiredStudyInstanceUid);

        ValidationWarnings warnings = ValidationWarnings.None;

        // validate input data elements
        if (_enableFullDicomItemValidation)
        {
            ValidateAllItems(dicomDataset);
        }
        else
        {
            warnings |= await ValidateIndexedItemsAsync(dicomDataset, cancellationToken);
        }

        // Validate for Implicit VR at the end
        if (ImplicitValueRepresentationValidator.IsImplicitVR(dicomDataset))
        {
            warnings |= ValidationWarnings.DatasetDoesNotMatchSOPClass;
        }

        return warnings;
    }

    private static void ValidateCoreTags(DicomDataset dicomDataset, string requiredStudyInstanceUid)
    {
        // Ensure required tags are present.
        EnsureRequiredTagIsPresent(DicomTag.PatientID);
        EnsureRequiredTagIsPresent(DicomTag.SOPClassUID);

        // The format of the identifiers will be validated by fo-dicom.
        string studyInstanceUid = EnsureRequiredTagIsPresent(DicomTag.StudyInstanceUID);
        string seriesInstanceUid = EnsureRequiredTagIsPresent(DicomTag.SeriesInstanceUID);
        string sopInstanceUid = EnsureRequiredTagIsPresent(DicomTag.SOPInstanceUID);

        // Ensure the StudyInstanceUid != SeriesInstanceUid != sopInstanceUid
        if (studyInstanceUid == seriesInstanceUid ||
            studyInstanceUid == sopInstanceUid ||
            seriesInstanceUid == sopInstanceUid)
        {
            throw new DatasetValidationException(
                FailureReasonCodes.ValidationFailure,
                DicomCoreResource.DuplicatedUidsNotAllowed);
        }

        // If the requestedStudyInstanceUid is specified, then the StudyInstanceUid must match.
        if (requiredStudyInstanceUid != null &&
            !studyInstanceUid.Equals(requiredStudyInstanceUid, StringComparison.OrdinalIgnoreCase))
        {
            throw new DatasetValidationException(
                FailureReasonCodes.MismatchStudyInstanceUid,
                string.Format(
                    CultureInfo.InvariantCulture,
                    DicomCoreResource.MismatchStudyInstanceUid,
                    studyInstanceUid,
                    requiredStudyInstanceUid));
        }

        string EnsureRequiredTagIsPresent(DicomTag dicomTag)
        {
            if (dicomDataset.TryGetSingleValue(dicomTag, out string value))
            {
                return value;
            }

            throw new DatasetValidationException(
                FailureReasonCodes.ValidationFailure,
                string.Format(
                    CultureInfo.InvariantCulture,
                    DicomCoreResource.MissingRequiredTag,
                    dicomTag.ToString()));
        }
    }

    private async Task<ValidationWarnings> ValidateIndexedItemsAsync(DicomDataset dicomDataset, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<QueryTag> queryTags = await _queryTagService.GetQueryTagsAsync(cancellationToken: cancellationToken);

        ValidationWarnings warning = ValidationWarnings.None;
        foreach (QueryTag queryTag in queryTags)
        {
            warning |= dicomDataset.ValidateQueryTag(queryTag, _minimumValidator);
        }
        return warning;
    }

    private static void ValidateAllItems(DicomDataset dicomDataset)
    {
        foreach (DicomItem item in dicomDataset)
        {
            try
            {
                item.Validate();
            }
            catch (DicomValidationException ex)
            {
                throw new DatasetValidationException(
                    FailureReasonCodes.ValidationFailure,
                    ex.Message,
                    ex);
            }
        }
    }
}
