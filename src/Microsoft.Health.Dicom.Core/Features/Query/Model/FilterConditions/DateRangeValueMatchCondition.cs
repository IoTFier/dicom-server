﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------
using System;
using Dicom;

namespace Microsoft.Health.Dicom.Core.Features.Query
{
    public class DateRangeValueMatchCondition : DicomQueryFilterCondition
    {
        internal DateRangeValueMatchCondition(DicomTag tag, DateTime minimum, DateTime maximum)
            : base(tag)
        {
            Minimum = minimum;
            Maximum = maximum;
        }

        public DateTime Minimum { get; set; }

        public DateTime Maximum { get; set; }

        public override void Accept(QueryFilterConditionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}