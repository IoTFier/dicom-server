# DICOM Conformance Statement

> This is currently a work-in progress document
> 

The **Azure for Health API** supports a subset of the DICOM Web standard. Support includes:

- [Store (STOW-RS)](#store-(stow-rs))
- [Retrieve (WADO-RS)](#retrieve-(wado-rs))
- [Search (QIDO-RS)](#search-(qido-rs))

Additionally, the following non-standard APIs are supported:

- [Delete](#delete)

## Store (STOW-RS)

This transaction uses the POST method to Store representations of Studies, Series, and Instances contained in the request payload.

| Method | Path               | Description |
| ------ | ------------------ | ----------- |
| POST   | ../studies         | Store instances. |
| POST   | ../studies/{study} | Store instances for a specific study. If an instance does not belong to the provided study identifier, that specific instance will be rejected with a '`43265`' warning code. |

Parameter `'study'` corresponds to the DICOM attribute StudyInstanceUID.

The following `'Accept'` headers for the response are supported:
- `application/dicom+json`

The following `'Content-Type'` headers are supported:
- `multipart/related; type=application/dicom`

> Note: The Server will <u>not</u> coerce or replace attributes that conflict with existing data. All data will be stored as provided.

The following DICOM elements are required to be present in every DICOM file attempting to be stored:
- StudyInstanceUID
- SeriesInstanceUID
- SopInstanceUID
- SOPClassUID
- PatientID

> Note: All identifiers must be between 1 and 64 characters long, and only contain alpha numeric characters or the following special characters: '.', '-'.

Each file stored must have a unique combination of StudyInstanceUID, SeriesInstanceUID and SopInstanceUID. The warning code `45070` will be returned in the result if a file with the same identifiers exists.

### Response Status Codes

| Code                         | Description |
| ---------------------------- | ----------- |
| 200 (OK)                     | When all the SOP instances in the request have been stored. |
| 202 (Accepted)               | When some instances in the request have been stored but others have failed. |
| 204 (No Content)             | No content was provided in the store transaction request. |
| 400 (Bad Request)            | The request was badly formatted. For example, the provided study instance identifier did not conform the expected UID format. |
| 406 (Not Acceptable)         | The specified `Accept` header is not supported. |
| 409 (Conflict)               | When none of the instances in the store transaction request have been stored. |
| 415 (Unsupported Media Type) | The provided `Content-Type` is not supported. |

### Response Payload

The response payload will populate a DICOM dataset with the following elements:

| Tag          | Name                  | Description |
| ------------ | --------------------- | ----------- |
| (0008, 1190) | RetrieveURL           | The Retrieve URL of the study if the StudyInstanceUID was provided in the store request. |
| (0008, 1198) | FailedSOPSequence     | The sequence of instances that failed to store. |
| (0008, 1199) | ReferencedSOPSequence | The sequence of stored instances. |

Each dataset in the `FailedSOPSequence` will have the following elements (if the DICOM file attempting to be stored could be read):

| Tag          | Name                     | Description |
| ------------ |------------------------- | ----------- |
| (0008, 1150) | ReferencedSOPClassUID    | The SOP class unique identifier of the instance that failed to store. |
| (0008, 1150) | ReferencedSOPInstanceUID | The SOP instance unique identifier of the instance that failed to store. |
| (0008, 1197) | FailureReason            | The reason code why this instance failed to store. |

Each dataset in the `ReferencedSOPSequence` will have the following elements:

| Tag          | Name                     | Description |
-------------- | ------------------------ | ----------- |
| (0008, 1150) | ReferencedSOPClassUID    | The SOP class unique identifier of the instance that failed to store. |
| (0008, 1150) | ReferencedSOPInstanceUID | The SOP instance unique identifier of the instance that failed to store. |
| (0008, 1190) | RetrieveURL              | The retrieve URL of this instance on the DICOM server. |

An example response with `Accept` header `application/dicom+json`:

```json
{
  "00081190":
  {
    "vr":"UR",
    "Value":["http://localhost/studies/d09e8215-e1e1-4c7a-8496-b4f6641ed232"]
  },
  "00081198":
  {
    "vr":"SQ",
    "Value":
    [{
      "00081150":
      {
        "vr":"UI","Value":["cd70f89a-05bc-4dab-b6b8-1f3d2fcafeec"]
      },
      "00081155":
      {
        "vr":"UI",
        "Value":["22c35d16-11ce-43fa-8f86-90ceed6cf4e7"]
      },
      "00081197":
      {
        "vr":"US",
        "Value":[43265]
      }
    }]
  },
  "00081199":
  {
    "vr":"SQ",
    "Value":
    [{
      "00081150":
      {
        "vr":"UI",
        "Value":["d246deb5-18c8-4336-a591-aeb6f8596664"]
      },
      "00081155":
      {
        "vr":"UI",
        "Value":["4a858cbb-a71f-4c01-b9b5-85f88b031365"]
      },
      "00081190":
      {
        "vr":"UR",
        "Value":["http://localhost/studies/d09e8215-e1e1-4c7a-8496-b4f6641ed232/series/8c4915f5-cc54-4e50-aa1f-9b06f6e58485/instances/4a858cbb-a71f-4c01-b9b5-85f88b031365"]
      }
    }]
  }
}
```

### Failure Reason Codes

| Code  | Description |
| ----- | ----------- |
| 272   | The store transaction did not store the instance because of a general failure in processing the operation. |
| 43264 | The DICOM instance failed the validation. |
| 43265 | The provided instance StudyInstanceUID did not match the specified StudyInstanceUID in the store request. |
| 45070 | A DICOM instance with the same StudyInstanceUID, SeriesInstanceUID and SopInstanceUID has already been stored. If you wish to update the contents, delete this instance first. |

## Retrieve (WADO-RS)

This Retrieve Transaction offers support for retrieving stored studies, series, instances and frames by reference.

The **Azure for Health** API supports the following methods:

| Method | Path                                                                  | Description |
| ------ | --------------------------------------------------------------------- | ----------- |
| GET    | ../study/{study}                                                      | Retrieves an entire study. |
| GET    | ../study/{study}/metadata                                             | Retrieves all the metadata for every instance in the study. |
| GET    | ../study/{study}/series/{series}                                      | Retrieves an series. |
| GET    | ../study/{study}/series/{series}/metadata                             | Retrieves all the metadata for every instance in the series. |
| GET    | ../study/{study}/series/{series}/instances/{instance}                 | Retrieves a single instance. |
| GET    | ../study/{study}/series/{series}/instances/{instance}/metadata        | Retrieves the metadata for a single instance. |
| GET    | ../study/{study}/series/{series}/instances/{instance}/frames/{frames} | Retrieves one or many frames from a single instance. To specify more than one frame, a comma seperate each frame to return, e.g. /study/1/series/2/instance/3/frames/4,5,6 |

### Retrieve Study or Series
The following `'Accept'` headers are supported for retrieving study or series:
- `multipart/related; type="application/dicom"; transfer-syntax=1.2.840.10008.1.2.1 (default)`
- `multipart/related; type="application/dicom"; transfer-syntax=*`

### Retrieve Metadata (for Study/ Series/ or Instance)
The following `'Accept'` headers are supported for retrieving metadata for a study, series or single instance:
- `application/dicom+json`

Retrieving metadata will not return attributes with the following value representations:
VR Name|Full
----------|----------
OB|Other Byte
OD|Other Double
OF|Other Float
OL|Other Long
OV|Other 64-Bit Very Long
OW|Other Word
UN|Unkown

### Retrieve Frames
The following `'Accept'` headers are supported for retrieving frames:
- `multipart/related; type="application/octet-stream"; transfer-syntax=1.2.840.10008.1.2.1 (default)`
- `multipart/related; type="application/octet-stream"; transfer-syntax=*`

> If the `'transfer-syntax'` header is not set, the Retrieve Transaction will default to 1.2.840.10008.1.2.1 (Little Endian Explicit). <br/> It is worth noting that if a file was uploaded using a compressed transfer syntax, by default, the result will be re-encoded. This could reduce the performance of the DICOM server on 'retrieve'. In this case, it is recommended to set the `transfer-syntax` header to **'`*`'**, or store all files as Little Endian explicit.

### Response Status Codes

| Code              | Description |
| ----------------- | ----------- |
| 200 (OK)          | All requested data has been retrieved. |
| 400 (Bad Request) | The request was badly formatted. For example, the provided study instance identifier did not conform the expected UID format or the requested transfer-syntax encoding is not supported. |
| 404 (Not Found)   | The specified DICOM resource could not be found. |

## Search (QIDO-RS)

Query based on ID for DICOM Objects (QIDO) enables you to search for studies, series and instances by attributes. 

Following **HTTP GET** endpoints are supported:

| Method | Path                                             | Description                       |
| ------ | ------------------------------------------------ | --------------------------------- |
|*Search for Studies*    |
| GET    | ../studies?...                                   | Search for studies                |
| *Search for Series*    |
| GET    | ../series?...                                    | Search for series                 |
| GET    |../studies/{study}/series?...                     | Search for series in a study      |
| *Search for Instances* |
| GET    |../instances?...                                  | Search for instances              |
| GET    |../studies/{study}/instances?...                  | Search for instances in a study   |
| GET    | ../studies/{study}/series/{series}/instances?... | Search for instances in a series  |

Accept Header Supported: `application/dicom+json`

### Supported Search Parameters
The following parameters for each query are supported:

Key|Support Value(s)|Allowed Count|Description
----------|----------|----------|----------
`{attributeID}=`|{value}|0...N|Search for attribute/ value matching in query.
`includefield=`|`{attributeID}`<br/>'`all`'|0...N|The additional attributes to return in the response.<br/>When '`all`' is provided, please see [Search Response](###Search-Response) for more information about which attributes will be returned for each query type.<br/>If a mixture of {attributeID} and 'all' is provided, the server will default to using 'all'.
`limit=`|{value}|0..1|Integer value to limit the number of values returned in the response.<br/>Value can be between the range 1 >= x <= 200. Defaulted to 100.
`offset=`|{value}|0..1|Skip {value} results.<br/>If an offset is provided larger than the number of search query results, a 204 (no content) response will be returned.
`fuzzymatching=`|true\|false|0..1|If true fuzzy matching is applied to PatientName attribute. It will do a prefix word match of any name part inside PatientName value.

#### Search Attributes
We support searching on below attributes and search type.

| Attribute Keyword | Study | Series | Instance |
| :--- | :---: | :---: | :---: |
| StudyInstanceId | X | X | X |
| PatientName | X | X | X |
| PatientID | X | X | X |
| AccessionNumber | X | X | X |
| ReferringPhysicianName | X | X | X |
| StudyDate | X | X | X |
| StudyDescription | X | X | X |
| SeriesInstanceUID |  | X | X |
| Modality |  | X | X |
| PerformedProcedureStepStartDate |  | X | X |
| SOPInstanceUID |  |  | X |


#### Search Matching
We support below matching types.

|Search Type|Supported Attribute|Example|
|----------|----------|----------|
|Range Query|StudyDate|{attributeID}={value1}-{value2}. For date/ time values, we supported an inclusive range on the tag. This will be mapped to `attributeID >= {value1} AND attributeID <= {value2}`.|
|Exact Match|All supported Atrributes |{attributeID}={value1}|
|Fuzzy Match|PatientName|Matches any component of the patientname which starts with the value|

#### Attribute ID

Tags can be encoded in a number of ways for the query parameter. We have partially implemented the standard as defined in [PS3.18 6.7.1.1.1](http://dicom.nema.org/medical/dicom/2019a/output/chtml/part18/sect_6.7.html#sect_6.7.1.1.1). The following encodings for a tag are supported:

Value|Example
----------|----------
{group}{element}|0020000D
{dicomKeyword}|StudyInstanceUID

Example query searching for instances: **../instances?Modality=CT&00280011=512&includefield=00280010&limit=5&offset=0**

### Search Response

The response will be an array of DICOM datasets. Depending on the resource , by *default* the following attributes are returned:

#### Study:
Attribute Name|Tag
----------|----------
Specific Character Set|(0008, 0005)
Study Date|(0008, 0020)
Study Time|(0008, 0030)
Accession Number|(0008, 0050)
Instance Availability|(0008, 0056)
Referring Physician Name|(0009, 0090)
Timezone Offset From UTC|(0008, 0201)
Patient Name|(0010, 0010)
Patient ID|(0010, 0020)
Patient Birth Date|(0010, 0030)
Patient Sex|(0010, 0040)
Study ID|(0020, 0010)
Study Instance UID|(0020, 000D)

#### Series:
Attribute Name|Tag
----------|----------
Specific Character Set|(0008, 0005)
Modality|(0008, 0060)
Timezone Offset From UTC|(0008, 0201)
Series Description|(0008, 103E) 
Series Instance UID|(0020, 000E)
Performed Procedure Step Start Date|(0040, 0244)
Performed Procedure Step Start Time|(0040, 0245)
Request Attributes Sequence|(0040, 0275)

#### Instance:
Attribute Name|Tag
----------|----------
Specific Character Set|(0008, 0005)
SOP Class UID|(0008, 0016)
SOP Instance UID|(0008, 0018)
Instance Availability|(0008, 0056)
Timezone Offset From UTC|(0008, 0201)
Instance Number|(0020, 0013)
Rows|(0028, 0010)
Columns|(0028, 0011)
Bits Allocated|(0028, 0100)
Number Of Frames|(0028, 0008)

If includefield=all, blew attributes are included along with default attributes. Along with default attributes, this is the full list of attributes supported at each resource level.

#### Study:
Attribute Name|
----------|
Study Description|
Anatomic Regions In Study Code Sequence|
Procedure Code Sequence|
Name Of Physicians Reading Study|
Admitting Diagnoses Description|
Referenced Study Sequence|
Patient Age|
Patient Size|
Patient Weight|
Occupation|
Additional Patient History|

#### Series:
Attribute Name|
----------|
Series Number|
Laterality|
Series Date|
Series Time|

Along with those below attributes are returned
- All the match query parameters and UIDs in the resource url.
- IncludeField attributes supported at that resource level. Not supported attributes will not be returned.
- If the target resource is All Series, then Study level attributes are also returned.
- If the target resource is All Instances, then Study and Series level attributes are also returned.
- If the target resource is Study's Instances, then Series level attributes are also returned.

### Response Codes

The query API will return one of the following status codes in the response:

Code|Name|Description
----------|----------|----------
*Success*|
200|OK|The response payload contains all the matching resource.
204|No Content|The search completed successfully but returned no results.
*Failure*|
400|Bad Request|The server was unable to perform the query because the query component was invalid. Response body contains details of the failure.
401|Unauthorized|The server refused to perform the query because the client is not authenticated.
503|Busy|Service is unavailable

### Additional Notes

- Querying using the `TimezoneOffsetFromUTC` (`00080201`) is not supported.
- The query API will not return 413 (request entity too large). If the requested query response limit is outside of the acceptable range, a bad request will be returned. Anything requested within the acceptable range, will be resolved.
- When target resource is Study/Series there is a potential for inconsistent study/series level metadata across multiple instances. For example, two instances could have different patientName. In this case we will return the study of either of the patientName match.
- Paged results are optimized to return matched *newest* instance first, this may result in duplicate records in subsequent pages if newer data matching the query was added.
- Matching on the strings is case in-sensitive and accent sensitive.

## Delete


This transaction is not part of the official DICOMweb standard. It uses the DELETE method to remove representations of Studies, Series, and Instances from the store.


| Method | Path                                                    | Description |
| ------ | ------------------------------------------------------- |------------ |
| DELETE | ../studies/{study}                                      | Delete all instances for a specific study. |
| DELETE | ../studies/{study}/series/{series}                      | Delete all instances for a specific series within a study. |
| DELETE | ../studies/{study}/series/{series}/instances/{instance} | Delete a specific instance within a series. |

Parameters `'study'`, `'series'` and `'instance'` correspond to the DICOM attributes StudyInstanceUID, SeriesInstanceUID and SopInstanceUID respectively.

There are no restrictions on the request's `'Accept'` header, `'Content-Type'` header or body content.

> Note: After a Delete transaction the deleted instances will not be recoverable.

### Response Status Codes

| Code              | Description |
| ----------------- | ------------ |
| 200 (OK)          | When all the SOP instances have been deleted. |
| 400 (Bad Request) | The request was badly formatted. |
| 404 (Not Found)   | When the specified series was not found within a study, or the specified instance was not found within the series. |

### Response Payload

The response body will be empty. The status code is the only useful information returned.
