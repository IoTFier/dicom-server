﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dicom;
using EnsureThat;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Options;
using Microsoft.Health.Blob.Configs;
using Microsoft.Health.Dicom.Core.Exceptions;
using Microsoft.Health.Dicom.Core.Extensions;
using Microsoft.Health.Dicom.Core.Features;
using Microsoft.Health.Dicom.Core.Features.Common;
using Microsoft.Health.Dicom.Core.Features.Model;
using Microsoft.Health.Dicom.Core.Web;
using Newtonsoft.Json;

namespace Microsoft.Health.Dicom.Metadata.Features.Storage
{
    /// <summary>
    /// Provides functionality for managing the DICOM instance metadata.
    /// </summary>
    public class BlobMetadataStore : IMetadataStore
    {
        private static readonly Encoding _metadataEncoding = Encoding.UTF8;
        private readonly CloudBlobContainer _container;
        private readonly JsonSerializer _jsonSerializer;

        public BlobMetadataStore(
            CloudBlobClient client,
            JsonSerializer jsonSerializer,
            IOptionsMonitor<BlobContainerConfiguration> namedBlobContainerConfigurationAccessor)
        {
            EnsureArg.IsNotNull(client, nameof(client));
            EnsureArg.IsNotNull(jsonSerializer, nameof(jsonSerializer));
            EnsureArg.IsNotNull(namedBlobContainerConfigurationAccessor, nameof(namedBlobContainerConfigurationAccessor));

            BlobContainerConfiguration containerConfiguration = namedBlobContainerConfigurationAccessor.Get(Constants.ContainerConfigurationName);

            _container = client.GetContainerReference(containerConfiguration.ContainerName);
            _jsonSerializer = jsonSerializer;
        }

        /// <inheritdoc />
        public async Task AddInstanceMetadataAsync(
            DicomDataset dicomDataset,
            long version,
            CancellationToken cancellationToken)
        {
            EnsureArg.IsNotNull(dicomDataset, nameof(dicomDataset));

            // Creates a copy of the dataset with bulk data removed.
            DicomDataset dicomDatasetWithoutBulkData = dicomDataset.CopyWithoutBulkDataItems();

            CloudBlockBlob blob = GetInstanceBlockBlob(dicomDatasetWithoutBulkData.ToVersionedInstanceIdentifier(version));

            blob.Properties.ContentType = KnownContentTypes.ApplicationJson;

            try
            {
                Stream stream = await blob.OpenWriteAsync(
                    AccessCondition.GenerateIfNotExistsCondition(),
                    new BlobRequestOptions(),
                    new OperationContext(),
                    cancellationToken);

                await using (stream)
                await using (var streamWriter = new StreamWriter(stream, _metadataEncoding))
                using (var jsonTextWriter = new JsonTextWriter(streamWriter))
                {
                    _jsonSerializer.Serialize(jsonTextWriter, dicomDatasetWithoutBulkData);
                }
            }
            catch (StorageException storageException) when (storageException.RequestInformation.HttpStatusCode == (int)HttpStatusCode.Conflict)
            {
                throw new InstanceAlreadyExistsException();
            }
            catch (Exception ex)
            {
                throw new DataStoreException(ex);
            }
        }

        /// <inheritdoc />
        public async Task DeleteInstanceMetadataIfExistsAsync(VersionedInstanceIdentifier versionedInstanceIdentifier, CancellationToken cancellationToken)
        {
            CloudBlockBlob blob = GetInstanceBlockBlob(versionedInstanceIdentifier);

            await ExecuteAsync(() => blob.DeleteIfExistsAsync(cancellationToken));
        }

        /// <inheritdoc />
        public async Task<DicomDataset> GetInstanceMetadataAsync(VersionedInstanceIdentifier versionedInstanceIdentifier, CancellationToken cancellationToken)
        {
            CloudBlockBlob cloudBlockBlob = GetInstanceBlockBlob(versionedInstanceIdentifier);

            DicomDataset dicomDataset = null;

            await ExecuteAsync(async () =>
            {
                await using (Stream stream = await cloudBlockBlob.OpenReadAsync(cancellationToken))
                using (var streamReader = new StreamReader(stream, _metadataEncoding))
                using (var jsonTextReader = new JsonTextReader(streamReader))
                {
                    dicomDataset = _jsonSerializer.Deserialize<DicomDataset>(jsonTextReader);
                }
            });

            return dicomDataset;
        }

        private CloudBlockBlob GetInstanceBlockBlob(VersionedInstanceIdentifier versionedInstanceIdentifier)
        {
            var blobName = $"{versionedInstanceIdentifier.StudyInstanceUid}/{versionedInstanceIdentifier.SeriesInstanceUid}/{versionedInstanceIdentifier.SopInstanceUid}_{versionedInstanceIdentifier.Version}_metadata.json";

            // Use the Azure storage SDK to validate the blob name; only specific values are allowed here.
            // Check here for more information: https://blogs.msdn.microsoft.com/jmstall/2014/06/12/azure-storage-naming-rules/
            NameValidator.ValidateBlobName(blobName);

            return _container.GetBlockBlobReference(blobName);
        }

        private async Task ExecuteAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (StorageException ex) when (ex.RequestInformation.HttpStatusCode == (int)HttpStatusCode.NotFound)
            {
                throw new InstanceNotFoundException();
            }
            catch (Exception ex)
            {
                throw new DataStoreException(ex);
            }
        }
    }
}
