﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using Dicom;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Health.Dicom.Core.Features.Query
{
    /// <summary>
    /// Parameter parsers
    /// </summary>
    public partial class QueryParser
    {
        private void ParseIncludeField(KeyValuePair<string, StringValues> queryParameter)
        {
            foreach (string value in queryParameter.Value)
            {
                var trimmedValue = value.Trim();
                if (IncludeFieldValueAll.Equals(trimmedValue, QueryParameterComparision))
                {
                    _parsedQuery.AllValue = true;
                    return;
                }

                if (TryParseDicomAttributeId(trimmedValue, out DicomTag dicomTag))
                {
                    _parsedQuery.IncludeFields.Add(dicomTag);
                    continue;
                }

                throw new QueryParseException(string.Format(DicomCoreResource.IncludeFieldUnknownAttribute, trimmedValue));
            }
        }

        private void ParseFuzzyMatching(KeyValuePair<string, StringValues> queryParameter)
        {
            var trimmedValue = queryParameter.Value.FirstOrDefault()?.Trim();
            if (bool.TryParse(trimmedValue, out bool result))
            {
                _parsedQuery.FuzzyMatch = result;
            }
            else
            {
                throw new QueryParseException(string.Format(DicomCoreResource.InvaludFuzzyMatchValue, trimmedValue));
            }
        }

        public void ParseOffset(KeyValuePair<string, StringValues> queryParameter)
        {
            var trimmedValue = queryParameter.Value.FirstOrDefault()?.Trim();
            if (int.TryParse(trimmedValue, out int result) && result >= 0)
            {
                _parsedQuery.Offset = result;
            }
            else
            {
                throw new QueryParseException(string.Format(DicomCoreResource.InvalidOffsetValue, trimmedValue));
            }
        }

        private void ParseLimit(KeyValuePair<string, StringValues> queryParameter)
        {
            var trimmedValue = queryParameter.Value.FirstOrDefault()?.Trim();
            if (int.TryParse(trimmedValue, out int result))
            {
                if (result > QueryLimit.MaxQueryResultCount || result < 1)
                {
                    throw new QueryParseException(string.Format(DicomCoreResource.QueryResultCountMaxExceeded, result, 1, QueryLimit.MaxQueryResultCount));
                }

                _parsedQuery.Limit = result;
            }
            else
            {
                throw new QueryParseException(string.Format(DicomCoreResource.InvalidLimitValue, trimmedValue));
            }
        }
    }
}
