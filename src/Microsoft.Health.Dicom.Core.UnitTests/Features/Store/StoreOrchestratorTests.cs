﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dicom;
using Microsoft.Health.Dicom.Core.Features.Common;
using Microsoft.Health.Dicom.Core.Features.Model;
using Microsoft.Health.Dicom.Core.Features.Store;
using Microsoft.Health.Dicom.Core.Features.Store.Entries;
using Microsoft.Health.Dicom.Core.Models;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Microsoft.Health.Dicom.Core.UnitTests.Features.Store
{
    public class StoreOrchestratorTests
    {
        private const string DefaultStudyInstanceUid = "1";
        private const string DefaultSeriesInstanceUid = "2";
        private const string DefaultSopInstanceUid = "3";
        private const long DefaultVersion = 1;
        private static readonly VersionedInstanceIdentifier _defaultVersionedInstanceIdentifier = new VersionedInstanceIdentifier(
            DefaultStudyInstanceUid,
            DefaultSeriesInstanceUid,
            DefaultSopInstanceUid,
            DefaultVersion);

        private static readonly CancellationToken DefaultCancellationToken = new CancellationTokenSource().Token;

        private readonly IFileStore _fileStore = Substitute.For<IFileStore>();
        private readonly IMetadataStore _metadataStore = Substitute.For<IMetadataStore>();
        private readonly IIndexDataStore _indexDataStore = Substitute.For<IIndexDataStore>();
        private readonly StoreOrchestrator _storeOrchestrator;

        private readonly DicomDataset _dicomDataset;
        private readonly Stream _stream = new MemoryStream();
        private readonly IDicomInstanceEntry _dicomInstanceEntry = Substitute.For<IDicomInstanceEntry>();

        public StoreOrchestratorTests()
        {
            _dicomDataset = new DicomDataset()
            {
                { DicomTag.StudyInstanceUID, DefaultStudyInstanceUid },
                { DicomTag.SeriesInstanceUID, DefaultSeriesInstanceUid },
                { DicomTag.SOPInstanceUID, DefaultSopInstanceUid },
            };

            _dicomInstanceEntry.GetDicomDatasetAsync(DefaultCancellationToken).Returns(_dicomDataset);
            _dicomInstanceEntry.GetStreamAsync(DefaultCancellationToken).Returns(_stream);

            _indexDataStore.CreateInstanceIndexAsync(_dicomDataset, DefaultCancellationToken).Returns(DefaultVersion);

            _storeOrchestrator = new StoreOrchestrator(_fileStore, _metadataStore, _indexDataStore);
        }

        [Fact]
        public async Task GivenFilesAreSuccessfullyStored_WhenStoreDicomInstanceEntryIsCalled_ThenStatusShouldBeUpdatedToCreated()
        {
            await _storeOrchestrator.StoreDicomInstanceEntryAsync(_dicomInstanceEntry, DefaultCancellationToken);

            await _indexDataStore.Received(1).UpdateInstanceIndexStatusAsync(
                Arg.Is<VersionedInstanceIdentifier>(identifier => _defaultVersionedInstanceIdentifier.Equals(identifier)),
                IndexStatus.Created,
                DefaultCancellationToken);
        }

        [Fact]
        public async Task GivenFailedToStoreFile_WhenStoreDicomInstanceEntryIsCalled_ThenCleanupShouldBeAttempted()
        {
            _fileStore.AddFileAsync(
                Arg.Is<VersionedInstanceIdentifier>(identifier => _defaultVersionedInstanceIdentifier.Equals(identifier)),
                _stream,
                overwriteIfExists: false,
                cancellationToken: DefaultCancellationToken)
                .Throws(new Exception());

            _indexDataStore.ClearReceivedCalls();

            await Assert.ThrowsAsync<Exception>(() => _storeOrchestrator.StoreDicomInstanceEntryAsync(_dicomInstanceEntry, DefaultCancellationToken));

            await ValidateCleanupAsync();

            await _indexDataStore.DidNotReceiveWithAnyArgs().UpdateInstanceIndexStatusAsync(default, default, default);
        }

        [Fact]
        public async Task GivenFailedToStoreMetadataFile_WhenStoreDicomInstanceEntryIsCalled_ThenCleanupShouldBeAttempted()
        {
            _metadataStore.AddInstanceMetadataAsync(
                _dicomDataset,
                DefaultVersion,
                DefaultCancellationToken)
                .Throws(new Exception());

            _indexDataStore.ClearReceivedCalls();

            await Assert.ThrowsAsync<Exception>(() => _storeOrchestrator.StoreDicomInstanceEntryAsync(_dicomInstanceEntry, DefaultCancellationToken));

            await ValidateCleanupAsync();

            await _indexDataStore.DidNotReceiveWithAnyArgs().UpdateInstanceIndexStatusAsync(default, default, default);
        }

        [Fact]
        public async Task GivenExceptionDuringCleanup_WhenStoreDicomInstanceEntryIsCalled_ThenItShouldNotInterfere()
        {
            _metadataStore.AddInstanceMetadataAsync(
                _dicomDataset,
                DefaultVersion,
                DefaultCancellationToken)
                .Throws(new ArgumentException());

            _indexDataStore.DeleteInstanceIndexAsync(default, default, default, default, default).ThrowsForAnyArgs(new InvalidOperationException());

            await Assert.ThrowsAsync<ArgumentException>(() => _storeOrchestrator.StoreDicomInstanceEntryAsync(_dicomInstanceEntry, DefaultCancellationToken));
        }

        private async Task ValidateCleanupAsync()
        {
            var timeout = DateTime.Now.AddSeconds(5);

            while (timeout < DateTime.Now)
            {
                if (_indexDataStore.ReceivedCalls().Any())
                {
                    await _indexDataStore.Received(1).DeleteInstanceIndexAsync(
                        DefaultStudyInstanceUid,
                        DefaultSeriesInstanceUid,
                        DefaultSopInstanceUid,
                        Arg.Any<DateTimeOffset>(),
                        CancellationToken.None);

                    break;
                }

                await Task.Delay(100);
            }
        }
    }
}
