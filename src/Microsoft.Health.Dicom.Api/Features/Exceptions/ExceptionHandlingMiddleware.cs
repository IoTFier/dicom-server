﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using EnsureThat;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Health.Abstractions.Exceptions;
using Microsoft.Health.Dicom.Core.Exceptions;
using NotSupportedException = Microsoft.Health.Dicom.Core.Exceptions.NotSupportedException;

namespace Microsoft.Health.Dicom.Api.Features.Exceptions
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            EnsureArg.IsNotNull(next, nameof(next));
            EnsureArg.IsNotNull(logger, nameof(logger));

            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            ExceptionDispatchInfo exceptionDispatchInfo = null;
            try
            {
                await _next(context);
            }
            catch (Exception exception)
            {
                if (context.Response.HasStarted)
                {
                    _logger.LogWarning("The response has already started, the base exception middleware will not be executed.");
                    throw;
                }

                // Get the Exception, but don't continue processing in the catch block as its bad for stack usage.
                exceptionDispatchInfo = ExceptionDispatchInfo.Capture(exception);
            }

            if (exceptionDispatchInfo != null)
            {
                var result = MapExceptionToResult(exceptionDispatchInfo.SourceException);
                await ExecuteResultAsync(context, result);
            }
        }

        private IActionResult MapExceptionToResult(Exception exception)
        {
            HttpStatusCode statusCode;
            string message = exception.Message;

            switch (exception)
            {
                case ValidationException _:
                case NotSupportedException _:
                    statusCode = HttpStatusCode.BadRequest;
                    break;
                case ResourceNotFoundException _:
                    statusCode = HttpStatusCode.NotFound;
                    break;
                case TranscodingException _:
                    statusCode = HttpStatusCode.NotAcceptable;
                    break;
                case DataStoreException _:
                    statusCode = HttpStatusCode.ServiceUnavailable;
                    break;
                case InstanceAlreadyExistsException _:
                    statusCode = HttpStatusCode.Conflict;
                    break;
                case DicomServerException _:
                    _logger.LogWarning("Service exception: {0}", exception);
                    statusCode = HttpStatusCode.ServiceUnavailable;
                    break;
                case UnsupportedMediaTypeException _:
                    statusCode = HttpStatusCode.UnsupportedMediaType;
                    break;
                case ServiceUnavailableException _:
                    statusCode = HttpStatusCode.ServiceUnavailable;
                    break;
                default:
                    _logger.LogError("Unhandled exception: {0}", exception);
                    statusCode = HttpStatusCode.InternalServerError;
                    message = string.Empty;
                    break;
            }

            return GetContentResult(statusCode, message);
        }

        private IActionResult GetContentResult(HttpStatusCode statusCode, string message)
        {
            return new ContentResult
            {
                StatusCode = (int)statusCode,
                Content = message,
            };
        }

        protected internal virtual async Task ExecuteResultAsync(HttpContext context, IActionResult result)
        {
            await result.ExecuteResultAsync(new ActionContext { HttpContext = context });
        }
    }
}
