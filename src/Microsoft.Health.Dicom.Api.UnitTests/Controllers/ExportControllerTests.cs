﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Net;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Health.Dicom.Api.Controllers;
using Microsoft.Health.Dicom.Core.Configs;
using Microsoft.Health.Dicom.Core.Messages.Export;
using Microsoft.Health.Dicom.Core.Models;
using Microsoft.Health.Dicom.Core.Models.Export;
using Microsoft.Health.Operations;
using Microsoft.Net.Http.Headers;
using NSubstitute;
using Xunit;

namespace Microsoft.Health.Dicom.Api.UnitTests.Controllers;

public class ExportControllerTests
{
    [Fact]
    public void GivenNullArguments_WhenConstructing_ThenThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ExportController(
            null,
            Options.Create(new FeatureConfiguration()),
            NullLogger<ExportController>.Instance));

        Assert.Throws<ArgumentNullException>(() => new ExportController(
            new Mediator(t => null),
            null,
            NullLogger<ExportController>.Instance));

        Assert.Throws<ArgumentNullException>(() => new ExportController(
            new Mediator(t => null),
            Options.Create<FeatureConfiguration>(null),
            NullLogger<ExportController>.Instance));

        Assert.Throws<ArgumentNullException>(() => new ExportController(
            new Mediator(t => null),
            Options.Create(new FeatureConfiguration()),
            null));
    }

    [Fact]
    public async Task GivenExportDisabled_WhenCallingApi_ThenShouldReturnNotFound()
    {
        IMediator _mediator = Substitute.For<IMediator>();
        var controller = new ExportController(
            _mediator,
            Options.Create(new FeatureConfiguration { EnableExport = false }),
            NullLogger<ExportController>.Instance);
        var spec = new ExportSpecification
        {
            Source = new TypedConfiguration<ExportSourceType> { Type = ExportSourceType.Identifiers },
            Destination = new TypedConfiguration<ExportDestinationType> { Type = ExportDestinationType.AzureBlob }
        };

        IActionResult result = await controller.ExportInstancesAsync(spec);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GivenExportEnabled_WhenCallingApi_ThenShouldReturnResult()
    {
        IMediator mediator = Substitute.For<IMediator>();
        var controller = new ExportController(
            mediator,
            Options.Create(new FeatureConfiguration { EnableExport = true }),
            NullLogger<ExportController>.Instance);

        controller.ControllerContext.HttpContext = new DefaultHttpContext();

        Guid operationId = Guid.NewGuid();
        var expected = new OperationReference(operationId, new Uri($"http://dicom.unit.test/operations/{operationId}"));
        var spec = new ExportSpecification
        {
            Source = new TypedConfiguration<ExportSourceType> { Type = ExportSourceType.Identifiers },
            Destination = new TypedConfiguration<ExportDestinationType> { Type = ExportDestinationType.AzureBlob }
        };

        mediator
            .Send(
                Arg.Is<ExportInstancesRequest>(x => ReferenceEquals(x.Specification, spec)),
                controller.HttpContext.RequestAborted)
            .Returns(new ExportInstancesResponse(expected));

        IActionResult result = await controller.ExportInstancesAsync(spec);
        Assert.IsType<ObjectResult>(result);

        var actual = result as ObjectResult;
        Assert.Equal((int)HttpStatusCode.Accepted, actual.StatusCode);
        Assert.True(controller.Response.Headers.TryGetValue(HeaderNames.Location, out StringValues header));
        Assert.Single(header);
        Assert.Same(expected, actual.Value);
        Assert.Equal(expected.Href.AbsoluteUri, header[0]);

        await mediator
            .Received(1)
            .Send(
                Arg.Is<ExportInstancesRequest>(x => ReferenceEquals(x.Specification, spec)),
                controller.HttpContext.RequestAborted);
    }
}
