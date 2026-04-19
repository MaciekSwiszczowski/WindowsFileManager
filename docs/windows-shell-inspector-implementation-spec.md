# Windows Shell Replacement Inspector – Implementation Spec

## Scope

This document is the **implementation reference** for the inspector. It is intended for both coding agents and developers doing manual bugfixing.

The implementation is split by category. Each category lists:

- exact APIs to call
- expected keys
- important flags / caveats
- direct links to method docs

## Global rules

1. Use Unicode Win32 APIs.
2. Open handles with maximal sharing: `FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE` unless a specific call requires otherwise.
3. For directory handles, open with `FILE_FLAG_BACKUP_SEMANTICS`.
4. For raw reparse inspection, open with `FILE_FLAG_OPEN_REPARSE_POINT`.
5. Never use mutating FSCTLs in the inspector.
6. Never remove, rewrite, or synthesize Mark of the Web / `Zone.Identifier` data from the inspector.
7. If a category fails, capture the error and continue.
8. Use `RetrievePropertiesAsync(null)` for the raw property bag.
9. Grouped inspector categories are long-lived UI objects. Update field values and field visibility in place; do not rebuild the category collection for every selection or deferred batch.
10. Unsupported selections such as multiselection or the synthetic parent row `..` must produce an empty inspector state. Selection-signature code must treat `..` as a synthetic token and must not dereference a missing backing model.
11. In the grouped inspector UI, render category contents with a simple two-column grid (`Property` fixed width, `Value` star width). Do not use nested `TableView` controls for grouped categories, because they can measure to desired width and prevent the value column from filling the available inspector space.
12. If the grouped inspector lives inside a `ScrollViewer`, host the categories inside one finite-width parent and prefer `ItemsRepeater` + `StackLayout` over nested `ItemsControl` presenters. The grouped host must determine width; templates must not rely on inner presenters to stretch rows.
13. Any shell-facing action that opens Explorer UI, including the standard Properties dialog, must pass the display path rather than the internal normalized `\\?\` path.

## Category 0 – Item acquisition

### APIs

- [`StorageFile`](<https://learn.microsoft.com/en-us/uwp/api/windows.storage.storagefile?view=winrt-28000>)
- [`StorageFolder`](<https://learn.microsoft.com/en-us/uwp/api/windows.storage.storagefolder?view=winrt-28000>)
- [`FileSystemInfo`](<https://learn.microsoft.com/en-us/dotnet/api/system.io.filesysteminfo?view=net-10.0>)
- [`FileInfo`](<https://learn.microsoft.com/en-us/dotnet/api/system.io.fileinfo?view=net-10.0>)
- [`CreateFileW`](<https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew>)
- [`Creating and opening files`](<https://learn.microsoft.com/en-us/windows/win32/fileio/creating-and-opening-files>)
- [`Obtaining a handle to a directory`](<https://learn.microsoft.com/en-us/windows/win32/fileio/obtaining-a-handle-to-a-directory>)

### Required behavior

- Build a cheap managed model first from `FileInfo` or `DirectoryInfo`.
- Create WinRT `StorageFile` / `StorageFolder` only when WinRT-only features are required: Property System, thumbnail, provider, cloud status.
- Open a native handle for all NTFS identity and low-level inspection.
- Use a second handle with `FILE_FLAG_OPEN_REPARSE_POINT` when reading raw reparse information.

### Handle recipes

| Scenario | Method(s) | Required flags / notes |
|---|---|---|
| File handle for identity / volume / final path | [`CreateFileW`](<https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew>) | `OPEN_EXISTING`, shared read/write/delete, usually `FILE_READ_ATTRIBUTES` access is enough. |
| Directory handle | [`CreateFileW`](<https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew>) + [`Obtaining a handle to a directory`](<https://learn.microsoft.com/en-us/windows/win32/fileio/obtaining-a-handle-to-a-directory>) | Add `FILE_FLAG_BACKUP_SEMANTICS`. |
| Raw reparse-point handle | [`CreateFileW`](<https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew>) + [`Reparse points and file operations`](<https://learn.microsoft.com/en-us/windows/win32/fileio/reparse-points-and-file-operations>) | Add `FILE_FLAG_OPEN_REPARSE_POINT`; keep `FILE_FLAG_BACKUP_SEMANTICS` for directories. |

## Category 1 – WinRT top-level, basic, and extended properties

### APIs

- [`Get file properties`](<https://learn.microsoft.com/en-us/windows/apps/develop/files/file-properties>)
- [`StorageFile.GetBasicPropertiesAsync`](<https://learn.microsoft.com/en-us/uwp/api/windows.storage.storagefile.getbasicpropertiesasync?view=winrt-26100>)
- [`BasicProperties`](<https://learn.microsoft.com/en-us/uwp/api/windows.storage.fileproperties.basicproperties?view=winrt-28000>)
- [`StorageFile.Properties`](<https://learn.microsoft.com/en-us/uwp/api/windows.storage.storagefile.properties?view=winrt-28000>)
- [`IStorageItemExtraProperties.RetrievePropertiesAsync`](<https://learn.microsoft.com/en-us/uwp/api/windows.storage.fileproperties.istorageitemextraproperties.retrievepropertiesasync?view=winrt-28000>)
- [`SystemProperties`](<https://learn.microsoft.com/en-us/uwp/api/windows.storage.systemproperties?view=winrt-28000>)

### Keys served from this category

- `Disp`, `Type`, `CType`, `IDate`, `Thumb`, `PropCt`, `Props`
- `LnTgt` and `LnkSt` for `.lnk` shortcuts via raw property keys
- Any extra `System.*` values not covered by dedicated categories

### Required methods

| Key(s) | Method(s) | Notes |
|---|---|---|
| `Disp`, `Type`, `CType` | [`StorageFile`](<https://learn.microsoft.com/en-us/uwp/api/windows.storage.storagefile?view=winrt-28000>) / [`StorageFolder`](<https://learn.microsoft.com/en-us/uwp/api/windows.storage.storagefolder?view=winrt-28000>) top-level properties | Use the direct WinRT properties first. |
| `IDate` | [`StorageFile.GetBasicPropertiesAsync`](<https://learn.microsoft.com/en-us/uwp/api/windows.storage.storagefile.getbasicpropertiesasync?view=winrt-26100>) | Read `BasicProperties.ItemDate`. |
| `Thumb` | [`StorageFile.GetThumbnailAsync`](<https://learn.microsoft.com/en-us/uwp/api/windows.storage.storagefile.getthumbnailasync?view=winrt-28000>) | For folders use `StorageFolder.GetThumbnailAsync` from the class page. |
| `PropCt`, `Props` | [`IStorageItemExtraProperties.RetrievePropertiesAsync`](<https://learn.microsoft.com/en-us/uwp/api/windows.storage.fileproperties.istorageitemextraproperties.retrievepropertiesasync?view=winrt-28000>) | Pass `null` to retrieve all possible properties. Store raw key/value pairs exactly as returned. |
| `LnTgt`, `LnkSt` for `.lnk` | [`RetrievePropertiesAsync`](<https://learn.microsoft.com/en-us/uwp/api/windows.storage.fileproperties.istorageitemextraproperties.retrievepropertiesasync?view=winrt-28000>) + [`System.Link.TargetParsingPath`](<https://learn.microsoft.com/en-us/windows/win32/properties/props-system-link-targetparsingpath>) + [`System.Link.Status`](<https://learn.microsoft.com/en-us/windows/win32/properties/props-system-link-status>) | Query raw Property System keys. |

### Required raw property keys

- `System.Link.TargetParsingPath`
- `System.Link.Status`
- `System.ParsingPath`
- `System.FileOwner`
- `System.DateAccessed`
- `System.ZoneIdentifier` when available
- provider / cloud keys when present

## Category 2 – Cheap managed file-system properties

### APIs

- [`FileSystemInfo`](<https://learn.microsoft.com/en-us/dotnet/api/system.io.filesysteminfo?view=net-10.0>)
- [`FileInfo`](<https://learn.microsoft.com/en-us/dotnet/api/system.io.fileinfo?view=net-10.0>)
- [`FileSystemInfo.LinkTarget`](<https://learn.microsoft.com/en-us/dotnet/api/system.io.filesysteminfo.linktarget?view=net-10.0>)

### Keys served from this category

- `Name`, `Ext`, `Path`, `Exist`, `Parent`, `Kind`
- `CTime`, `MTime`, `ATime`, `Size`
- `Attr`
- `LnTgt` for symlink / junction scenarios

### Required methods

| Key(s) | Method(s) | Notes |
|---|---|---|
| identity and timestamps | [`FileSystemInfo`](<https://learn.microsoft.com/en-us/dotnet/api/system.io.filesysteminfo?view=net-10.0>) | Use `Name`, `FullName`, `Exists`, `CreationTime`, `LastWriteTime`, `LastAccessTime`, `Attributes`. |
| `Size` | [`FileInfo`](<https://learn.microsoft.com/en-us/dotnet/api/system.io.fileinfo?view=net-10.0>) | Use `Length`; folders have no managed logical size. |
| `LnTgt` | [`FileSystemInfo.LinkTarget`](<https://learn.microsoft.com/en-us/dotnet/api/system.io.filesysteminfo.linktarget?view=net-10.0>) | Covers symlinks and junctions, not `.lnk` shell shortcuts. |

## Category 2A – NTFS attribute flags

### APIs

- [`FileSystemInfo`](<https://learn.microsoft.com/en-us/dotnet/api/system.io.filesysteminfo?view=net-10.0>)
- [`FileInfo`](<https://learn.microsoft.com/en-us/dotnet/api/system.io.fileinfo?view=net-10.0>)

### Keys served from this category

- `RO`, `Hid`, `Sys`, `Arc`, `Tmp`, `Offl`, `NIdx`, `Efs`, `Cmp`, `Sprs`, `RPt`

### Required methods

| Key(s) | Method(s) | Notes |
|---|---|---|
| all NTFS flags | [`FileSystemInfo.Attributes`](<https://learn.microsoft.com/en-us/dotnet/api/system.io.filesysteminfo.attributes?view=net-10.0>) | Derive each flag as a simple Yes/No boolean from the current file-system attributes. This category is immediate and must not wait on any background handle-based diagnostic. |

## Category 3 – IDs / stable item identity and final path

### APIs

- [`GetFileInformationByHandleEx`](<https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getfileinformationbyhandleex>)
- [`FILE_ID_INFO`](<https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_id_info>)
- [`GetFileInformationByHandle`](<https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfileinformationbyhandle>)
- [`BY_HANDLE_FILE_INFORMATION`](<https://learn.microsoft.com/en-us/windows/win32/api/fileapi/ns-fileapi-by_handle_file_information>)
- [`GetFinalPathNameByHandleW`](<https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfinalpathnamebyhandlew>)
- [`FILE_ATTRIBUTE_TAG_INFO`](<https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_attribute_tag_info>)

### Keys served from this category

- `FileId`, `VolSN`, `Id64`, `Links`, `FPath`, `RTag`, `Attr`

### Required methods

| Key(s) | Method(s) | Notes |
|---|---|---|
| `FileId`, `VolSN` | [`GetFileInformationByHandleEx`](<https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getfileinformationbyhandleex>) with `FileIdInfo` -> [`FILE_ID_INFO`](<https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_id_info>) | This is the primary file/dir identity. |
| `Id64`, `Links` | [`GetFileInformationByHandle`](<https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfileinformationbyhandle>) -> [`BY_HANDLE_FILE_INFORMATION`](<https://learn.microsoft.com/en-us/windows/win32/api/fileapi/ns-fileapi-by_handle_file_information>) | Use only as a compatibility diagnostic. |
| `FPath` | [`GetFinalPathNameByHandleW`](<https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfinalpathnamebyhandlew>) | Resolve final normalized path from the native handle. |
| `RTag`, low-level `Attr` | [`GetFileInformationByHandleEx`](<https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getfileinformationbyhandleex>) with `FileAttributeTagInfo` -> [`FILE_ATTRIBUTE_TAG_INFO`](<https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_attribute_tag_info>) | Prefer this over guessing from path text. |

## Category 4 – Streams, compression, sparse allocation

### APIs

- [`File streams`](<https://learn.microsoft.com/en-us/windows/win32/fileio/file-streams>)
- [`FindFirstStreamW`](<https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-findfirststreamw>)
- [`FindNextStreamW`](<https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-findnextstreamw>)
- [`GetCompressedFileSizeW`](<https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getcompressedfilesizew>)

### Keys served from this category

- `ADS`, `ADSCnt`, `ADSList`, `CSize`

### Required methods

| Key(s) | Method(s) | Notes |
|---|---|---|
| `ADS`, `ADSCnt`, `ADSList` | [`FindFirstStreamW`](<https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-findfirststreamw>) + [`FindNextStreamW`](<https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-findnextstreamw>) | Exclude the default unnamed stream from the named-stream count. Directories may also have named streams. |
| `CSize` | [`GetCompressedFileSizeW`](<https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getcompressedfilesizew>) | Use as the actual on-disk byte count for compressed or sparse files. |

## Category 5 – Trust / origin / Mark of the Web

### APIs

- [`Attachment Manager in Microsoft Windows`](<https://support.microsoft.com/en-us/topic/information-about-the-attachment-manager-in-microsoft-windows-c48a4dcd-8de5-2af5-ee9b-cd795ae42738>)
- [`Policy CSP - AttachmentManager`](<https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-attachmentmanager>)
- [`File streams`](<https://learn.microsoft.com/en-us/windows/win32/fileio/file-streams>)
- [`CreateFileW`](<https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew>)
- [`5.6.1 Zone.Identifier Stream Name - MS-FSCC`](<https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-fscc/6e3f7352-d11c-4d76-8c39-2516a9df36e8>)
- [`System.ZoneIdentifier`](<https://learn.microsoft.com/en-us/windows/win32/properties/props-system-zoneidentifier>)

### Keys served from this category

- `Blk`, `Zone`, `ZAds`, `ZTxt`

### Required methods

| Key(s) | Method(s) | Notes |
|---|---|---|
| `ZAds` | [`FindFirstStreamW`](<https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-findfirststreamw>) + [`FindNextStreamW`](<https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-findnextstreamw>) | Detect whether `Zone.Identifier` exists among named alternate data streams. Reuse cached stream enumeration from Category 4 when available. |
| `ZTxt` | [`CreateFileW`](<https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew>) + [`File streams`](<https://learn.microsoft.com/en-us/windows/win32/fileio/file-streams>) | Open `<path>:Zone.Identifier` read-only and read the text payload exactly as stored. |
| `Zone` | parse `ZoneId=` from the `Zone.Identifier` text payload; optionally cross-check with [`System.ZoneIdentifier`](<https://learn.microsoft.com/en-us/windows/win32/properties/props-system-zoneidentifier>) when the Property System exposes it | Surface both raw numeric value and a friendly label when known. |
| `Blk` | derive from `Zone.Identifier` presence and parsed zone value | `Blk` means Windows-origin trust marking exists. It is not the same as cloud availability, `FILE_ATTRIBUTE_OFFLINE`, or a normal file attribute. |

### Hard rules

- Do **not** conflate Mark of the Web with cloud placeholder state. `Blk` and `Zone` are separate from `Avail`, `CfState`, `Pin`, and `Offl`.
- Do **not** claim `Blk` is an NTFS file attribute. It is Attachment Manager / zone-of-origin state, usually backed by the `Zone.Identifier` ADS.
- The inspector is read-only here. Never remove the stream and never call `Unblock-File` or equivalent from the inspector.

## Category 6 – Volume context

### APIs

- [`Obtaining volume information`](<https://learn.microsoft.com/en-us/windows/win32/fileio/obtaining-volume-information>)
- [`File attribute constants`](<https://learn.microsoft.com/en-us/windows/win32/fileio/file-attribute-constants>)

### Keys served from this category

- `Fs`, `FsCap`

### Required methods

- Call `GetVolumeInformation` for the hosting volume.
- Interpret the returned file-system flags into readable capability badges.
- At minimum surface: ACLs, EFS, compression, sparse files, reparse points, named streams, object IDs, case-sensitive search, and unicode-on-disk support when relevant.

## Category 7 – Reparse points, junctions, mount points, and shell links

### APIs

- [`Reparse points`](<https://learn.microsoft.com/en-us/windows/win32/fileio/reparse-points>)
- [`Reparse point operations`](<https://learn.microsoft.com/en-us/windows/win32/fileio/reparse-point-operations>)
- [`Reparse points and file operations`](<https://learn.microsoft.com/en-us/windows/win32/fileio/reparse-points-and-file-operations>)
- [`DeviceIoControl`](<https://learn.microsoft.com/en-us/windows/win32/api/ioapiset/nf-ioapiset-deviceiocontrol>)
- [`Directory management control codes`](<https://learn.microsoft.com/en-us/windows/win32/fileio/directory-management-control-codes>)
- [`Shell links`](<https://learn.microsoft.com/en-us/windows/win32/shell/links>)
- [`System.Link.TargetParsingPath`](<https://learn.microsoft.com/en-us/windows/win32/properties/props-system-link-targetparsingpath>)
- [`System.Link.Status`](<https://learn.microsoft.com/en-us/windows/win32/properties/props-system-link-status>)

### Keys served from this category

- `LnTgt`, `LnkSt`, `RTag`, `RData`

### Required methods

| Key(s) | Method(s) | Notes |
|---|---|---|
| `LnTgt` for symlink/junction | [`FileSystemInfo.LinkTarget`](<https://learn.microsoft.com/en-us/dotnet/api/system.io.filesysteminfo.linktarget?view=net-10.0>) | Cheap first pass. |
| `RTag` | [`GetFileInformationByHandleEx`](<https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getfileinformationbyhandleex>) with `FileAttributeTagInfo` | Always use a handle-based result. |
| `RData` | [`DeviceIoControl`](<https://learn.microsoft.com/en-us/windows/win32/api/ioapiset/nf-ioapiset-deviceiocontrol>) + [`FSCTL_GET_REPARSE_POINT`](<https://learn.microsoft.com/en-us/windows/win32/fileio/directory-management-control-codes>) | Only on a handle opened with `FILE_FLAG_OPEN_REPARSE_POINT`. |
| `.lnk` target/status | [`RetrievePropertiesAsync`](<https://learn.microsoft.com/en-us/uwp/api/windows.storage.fileproperties.istorageitemextraproperties.retrievepropertiesasync?view=winrt-28000>) + Property keys | Prefer `System.Link.TargetParsingPath` and `System.Link.Status` for read-only inspection. |

## Category 8 – Security descriptor and ACL summary

### APIs

- [`Access control model`](<https://learn.microsoft.com/en-us/windows/win32/secauthz/access-control-model>)
- [`.NET ACL how-to`](<https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-add-or-remove-access-control-list-entries>)
- [`FileSystemAclExtensions.GetAccessControl`](<https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemaclextensions.getaccesscontrol?view=net-10.0>)
- [`FileSystemSecurity`](<https://learn.microsoft.com/en-us/dotnet/api/system.security.accesscontrol.filesystemsecurity?view=net-10.0>)

### Keys served from this category

- `Owner`, `Group`, `DACL`, `SACL`, `Inh`, `Prot`

### Required methods

| Key(s) | Method(s) | Notes |
|---|---|---|
| all security keys | [`FileSystemAclExtensions.GetAccessControl`](<https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemaclextensions.getaccesscontrol?view=net-10.0>) | Use `FileSecurity` or `DirectorySecurity` depending on item kind. |
| rule summaries | [`FileSystemSecurity`](<https://learn.microsoft.com/en-us/dotnet/api/system.security.accesscontrol.filesystemsecurity?view=net-10.0>) | Summarize, do not dump raw ACE text in the default view. |

## Category 9 – In-use / lock diagnostics

### APIs

- [`Restart Manager`](<https://learn.microsoft.com/en-us/windows/win32/rstmgr/restart-manager-portal>)
- [`RmStartSession`](<https://learn.microsoft.com/en-us/windows/win32/api/restartmanager/nf-restartmanager-rmstartsession>)
- [`RmRegisterResources`](<https://learn.microsoft.com/en-us/windows/win32/api/restartmanager/nf-restartmanager-rmregisterresources>)
- [`RmGetList`](<https://learn.microsoft.com/en-us/windows/win32/api/restartmanager/nf-restartmanager-rmgetlist>)
- [`RM_PROCESS_INFO`](<https://learn.microsoft.com/en-us/windows/win32/api/restartmanager/ns-restartmanager-rm_process_info>)
- [`IFileIsInUse`](<https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-ifileisinuse>)
- [`IFileIsInUse::GetAppName`](<https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileisinuse-getappname>)
- [`IFileIsInUse::GetCapabilities`](<https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileisinuse-getcapabilities>)
- [`IFileIsInUse::GetSwitchToHWND`](<https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileisinuse-getswitchtohwnd>)

### Keys served from this category

- `InUse`, `LockBy`, `LockPid`, `LockSvc`, `Usage`, `CanSw`, `CanCls`

### Required methods

| Key(s) | Method(s) | Notes |
|---|---|---|
| `InUse`, `LockBy`, `LockPid`, `LockSvc` | [`RmStartSession`](<https://learn.microsoft.com/en-us/windows/win32/api/restartmanager/nf-restartmanager-rmstartsession>) -> [`RmRegisterResources`](<https://learn.microsoft.com/en-us/windows/win32/api/restartmanager/nf-restartmanager-rmregisterresources>) -> [`RmGetList`](<https://learn.microsoft.com/en-us/windows/win32/api/restartmanager/nf-restartmanager-rmgetlist>) -> [`RM_PROCESS_INFO`](<https://learn.microsoft.com/en-us/windows/win32/api/restartmanager/ns-restartmanager-rm_process_info>) | Primary supported mechanism for 'who is using this file'. |
| `Usage`, `CanSw`, `CanCls` | [`IFileIsInUse`](<https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-ifileisinuse>) and its methods | Optional enrichment when available. |

### Hard rule

- Do **not** claim `locked by DLL`. The supported inspector result is the locking **process / service / application**.

## Category 10 – Cloud provider and placeholder state

### APIs

- [`Cloud Sync Engines`](<https://learn.microsoft.com/en-us/windows/win32/cfapi/cloud-files-api-portal>)
- [`Build a Cloud Sync Engine that Supports Placeholder Files`](<https://learn.microsoft.com/en-us/windows/win32/cfapi/build-a-cloud-file-sync-engine>)
- [`Determining availability of Microsoft OneDrive files`](<https://learn.microsoft.com/en-us/windows/apps/develop/files/determine-availability-microsoft-onedrive-files>)
- [`IStorageItemPropertiesWithProvider.Provider`](<https://learn.microsoft.com/en-us/uwp/api/windows.storage.istorageitempropertieswithprovider.provider?view=winrt-28000>)
- [`StorageFile.IsAvailable`](<https://learn.microsoft.com/en-us/uwp/api/windows.storage.storagefile.isavailable?view=winrt-28000>)
- [`StorageProviderSyncRootManager.GetSyncRootInformationForFolder`](<https://learn.microsoft.com/en-us/uwp/api/windows.storage.provider.storageprovidersyncrootmanager.getsyncrootinformationforfolder?view=winrt-28000>)
- [`StorageProviderSyncRootInfo`](<https://learn.microsoft.com/en-us/uwp/api/windows.storage.provider.storageprovidersyncrootinfo?view=winrt-28000>)
- [`StorageProviderItemProperty`](<https://learn.microsoft.com/en-us/uwp/api/windows.storage.provider.storageprovideritemproperty?view=winrt-28000>)
- [`StorageProviderItemProperties.SetAsync`](<https://learn.microsoft.com/en-us/uwp/api/windows.storage.provider.storageprovideritemproperties.setasync?view=winrt-28000>)
- [`Cloud Files functions`](<https://learn.microsoft.com/en-us/windows/win32/cfapi/cloud-files-functions>)
- [`CfGetPlaceholderStateFromFileInfo`](<https://learn.microsoft.com/en-us/windows/win32/api/cfapi/nf-cfapi-cfgetplaceholderstatefromfileinfo>)
- [`CfGetPlaceholderInfo`](<https://learn.microsoft.com/en-us/windows/win32/api/cfapi/nf-cfapi-cfgetplaceholderinfo>)

### Keys served from this category

- `Prov`, `SyncRt`, `SyncId`, `ProvId`, `Avail`, `CfState`, `CfInfo`, `CusSt`, plus cloud-related flag keys (`Pin`, `Unpin`, `RcOpen`, `RcData`) when present

### Required methods

| Key(s) | Method(s) | Notes |
|---|---|---|
| `Prov` | [`IStorageItemPropertiesWithProvider.Provider`](<https://learn.microsoft.com/en-us/uwp/api/windows.storage.istorageitempropertieswithprovider.provider?view=winrt-28000>) | Cheap provider identity. |
| `Avail` | [`StorageFile.IsAvailable`](<https://learn.microsoft.com/en-us/uwp/api/windows.storage.storagefile.isavailable?view=winrt-28000>) | File-only WinRT availability. |
| `SyncRt`, `SyncId`, `ProvId` | [`StorageProviderSyncRootManager.GetSyncRootInformationForFolder`](<https://learn.microsoft.com/en-us/uwp/api/windows.storage.provider.storageprovidersyncrootmanager.getsyncrootinformationforfolder?view=winrt-28000>) -> [`StorageProviderSyncRootInfo`](<https://learn.microsoft.com/en-us/uwp/api/windows.storage.provider.storageprovidersyncrootinfo?view=winrt-28000>) | Walk up to the owning sync root if needed. |
| `CfState` | [`CfGetPlaceholderStateFromFileInfo`](<https://learn.microsoft.com/en-us/windows/win32/api/cfapi/nf-cfapi-cfgetplaceholderstatefromfileinfo>) | Feed it data from handle-based file info. |
| `CfInfo` | [`CfGetPlaceholderInfo`](<https://learn.microsoft.com/en-us/windows/win32/api/cfapi/nf-cfapi-cfgetplaceholderinfo>) | Advanced placeholder details. |
| `CusSt` | raw Property System + provider-specific values | `StorageProviderItemProperty` documents the provider-defined state shape; reading is usually via WinRT property surfaces / Property System, not via `SetAsync`. |

## Category 11 – Thumbnails and handler diagnostics

### APIs

- [`StorageFile.GetThumbnailAsync`](<https://learn.microsoft.com/en-us/uwp/api/windows.storage.storagefile.getthumbnailasync?view=winrt-28000>)
- [`File types and file associations`](<https://learn.microsoft.com/en-us/windows/win32/shell/fa-intro>)
- [`How file associations work`](<https://learn.microsoft.com/en-us/windows/win32/shell/fa-how-work>)
- [`Programmatic Identifiers`](<https://learn.microsoft.com/en-us/windows/win32/shell/fa-progids>)
- [`AssocQueryString`](<https://learn.microsoft.com/en-us/windows/win32/api/shlwapi/nf-shlwapi-assocquerystringa>)
- [`ASSOCSTR`](<https://learn.microsoft.com/en-us/windows/win32/api/shlwapi/ne-shlwapi-assocstr>)
- [`IQueryAssociations`](<https://learn.microsoft.com/en-us/windows/win32/api/shlwapi/nn-shlwapi-iqueryassociations>)
- [`SHAssocEnumHandlers`](<https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-shassocenumhandlers>)
- [`IEnumAssocHandlers`](<https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-ienumassochandlers>)
- [`Thumbnail handlers`](<https://learn.microsoft.com/en-us/windows/win32/shell/thumbnail-providers>)
- [`IThumbnailProvider`](<https://learn.microsoft.com/en-us/windows/win32/api/thumbcache/nn-thumbcache-ithumbnailprovider>)
- [`Preview handlers`](<https://learn.microsoft.com/en-us/windows/win32/shell/preview-handlers>)
- [`Implementing property handlers`](<https://learn.microsoft.com/en-us/windows/win32/properties/building-property-handlers>)
- [`Property sheet handlers`](<https://learn.microsoft.com/en-us/windows/win32/shell/propsheet-handlers>)
- [`Icons and icon overlays`](<https://learn.microsoft.com/en-us/windows/win32/shell/icons-and-icon-overlays-bumper>)

### Keys served from this category

- `Thumb`, `ProgId`, `ThCls`, `PrCls`, `PhCls`, `OvCls`, `PsCls`

### Required methods

| Key(s) | Method(s) | Notes |
|---|---|---|
| `Thumb` | [`StorageFile.GetThumbnailAsync`](<https://learn.microsoft.com/en-us/uwp/api/windows.storage.storagefile.getthumbnailasync?view=winrt-28000>) | Display the thumbnail itself. |
| `ProgId` | [`AssocQueryString`](<https://learn.microsoft.com/en-us/windows/win32/api/shlwapi/nf-shlwapi-assocquerystringa>) with relevant [`ASSOCSTR`](<https://learn.microsoft.com/en-us/windows/win32/api/shlwapi/ne-shlwapi-assocstr>) values | Resolve association identity from extension. |
| `ThCls`, `PrCls`, `PhCls`, `OvCls`, `PsCls` | [`SHAssocEnumHandlers`](<https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-shassocenumhandlers>) + [`IEnumAssocHandlers`](<https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-ienumassochandlers>) and/or registry-backed association inspection guided by file-association docs | Surface registered handlers, not runtime provenance. |

### Hard rules

- `ThCls` is **registered handler data** only.
- The app must not say 'this handler produced the current thumbnail' unless Windows exposes that directly in a supported API in the future.

## Category 12 – PE / version-resource details

### APIs

- [`FileVersionInfo`](<https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.fileversioninfo?view=net-10.0>)

### Keys served from this category

- `Co`, `Prod`, `FVer`, `PVer`, `Orig`, `Int`, `Dbg`, `Pre`, `Priv`, `Spec`

### Required methods

- Use `FileVersionInfo.GetVersionInfo(path)` only for PE files or when the file extension strongly suggests a Win32 version resource may exist.
- If no version resource exists, show `N/A` for the whole group.

## Category 13 – Optional NTFS/ReFS advanced diagnostics

### APIs

- [`Distributed link tracking and object identifiers`](<https://learn.microsoft.com/en-us/windows/win32/fileio/distributed-link-tracking-and-object-identifiers>)
- [`FSCTL_GET_OBJECT_ID`](<https://learn.microsoft.com/en-us/windows/win32/api/winioctl/ni-winioctl-fsctl_get_object_id>)
- [`FSCTL_GET_INTEGRITY_INFORMATION`](<https://learn.microsoft.com/en-us/windows/win32/api/winioctl/ni-winioctl-fsctl_get_integrity_information>)
- [`ReFS overview`](<https://learn.microsoft.com/en-us/windows-server/storage/refs/refs-overview>)

### Keys served from this category

- `ObjId`, `Intgr`

### Required methods

| Key(s) | Method(s) | Notes |
|---|---|---|
| `ObjId` | [`DeviceIoControl`](<https://learn.microsoft.com/en-us/windows/win32/api/ioapiset/nf-ioapiset-deviceiocontrol>) + [`FSCTL_GET_OBJECT_ID`](<https://learn.microsoft.com/en-us/windows/win32/api/winioctl/ni-winioctl-fsctl_get_object_id>) | Read-only. Do not use `FSCTL_CREATE_OR_GET_OBJECT_ID` in the inspector. |
| `Intgr` | [`DeviceIoControl`](<https://learn.microsoft.com/en-us/windows/win32/api/ioapiset/nf-ioapiset-deviceiocontrol>) + [`FSCTL_GET_INTEGRITY_INFORMATION`](<https://learn.microsoft.com/en-us/windows/win32/api/winioctl/ni-winioctl-fsctl_get_integrity_information>) | Only where supported, typically ReFS. |

## Category 14 – Raw property bag

### APIs

- [`Property System Overview`](<https://learn.microsoft.com/en-us/windows/win32/properties/property-system-overview>)
- [`Windows Property System`](<https://learn.microsoft.com/en-us/windows/win32/properties/windows-properties-system>)
- [`IStorageItemExtraProperties.RetrievePropertiesAsync`](<https://learn.microsoft.com/en-us/uwp/api/windows.storage.fileproperties.istorageitemextraproperties.retrievepropertiesasync?view=winrt-28000>)

### Keys served from this category

- `PropCt`, `Props`

### Required methods

- Call `RetrievePropertiesAsync(null)`.
- Preserve key names exactly as returned.
- Preserve null values distinctly from missing values.
- Sort keys ordinally when rendering the advanced raw-property view.

## Key mapping summary

| Key | Primary API family |
|---|---|
| `Name` | Managed .NET |
| `Disp` | WinRT |
| `Type` | WinRT / Property System |
| `CType` | WinRT |
| `Ext` | Managed .NET |
| `Path` | Managed .NET |
| `FPath` | Win32 handle |
| `Exist` | Managed .NET |
| `Parent` | Managed .NET |
| `Kind` | Managed .NET |
| `CTime` | Managed .NET |
| `MTime` | Managed .NET + WinRT |
| `ATime` | Managed .NET |
| `IDate` | WinRT BasicProperties |
| `Size` | Managed .NET / WinRT |
| `CSize` | Win32 |
| `Attr` | Managed .NET / Win32 |
| `RO` | Managed .NET / Win32 NTFS flags |
| `Hid` | Managed .NET / Win32 NTFS flags |
| `Sys` | Managed .NET / Win32 NTFS flags |
| `Arc` | Managed .NET / Win32 NTFS flags |
| `Tmp` | Managed .NET / Win32 NTFS flags |
| `Offl` | Win32 NTFS flags |
| `NIdx` | Win32 NTFS flags |
| `Efs` | Win32 NTFS flags |
| `Cmp` | Win32 NTFS flags |
| `Sprs` | Win32 NTFS flags |
| `RPt` | Win32 NTFS flags |
| `Pin` | Win32 / CfApi |
| `Unpin` | Win32 / CfApi |
| `RcOpen` | Win32 / CfApi |
| `RcData` | Win32 / CfApi |
| `FileId` | Win32 handle |
| `VolSN` | Win32 handle |
| `Id64` | Win32 handle |
| `Links` | Win32 handle |
| `LnTgt` | Managed .NET / Property System |
| `LnkSt` | Property System |
| `RTag` | Win32 handle |
| `RData` | Win32 FSCTL |
| `ObjId` | Win32 FSCTL |
| `ADS` | Win32 streams |
| `ADSCnt` | Win32 streams |
| `ADSList` | Win32 streams |
| `Blk` | ADS + Attachment Manager semantics |
| `Zone` | ADS / Property System |
| `ZAds` | Win32 streams |
| `ZTxt` | Win32 streams / direct stream read |
| `Owner` | Managed ACL |
| `Group` | Managed ACL |
| `DACL` | Managed ACL |
| `SACL` | Managed ACL |
| `Inh` | Managed ACL |
| `Prot` | Managed ACL |
| `InUse` | Restart Manager |
| `LockBy` | Restart Manager / IFileIsInUse |
| `LockPid` | Restart Manager |
| `LockSvc` | Restart Manager |
| `Usage` | IFileIsInUse |
| `CanSw` | IFileIsInUse |
| `CanCls` | IFileIsInUse |
| `Prov` | WinRT |
| `SyncRt` | WinRT provider |
| `SyncId` | WinRT provider |
| `ProvId` | WinRT provider |
| `Avail` | WinRT |
| `CfState` | CfApi |
| `CfInfo` | CfApi |
| `CusSt` | WinRT / Property System |
| `Thumb` | WinRT |
| `ProgId` | Shell association |
| `ThCls` | Shell association |
| `PrCls` | Shell association |
| `PhCls` | Shell association |
| `OvCls` | Shell association |
| `PsCls` | Shell association |
| `Co` | FileVersionInfo |
| `Prod` | FileVersionInfo |
| `FVer` | FileVersionInfo |
| `PVer` | FileVersionInfo |
| `Orig` | FileVersionInfo |
| `Int` | FileVersionInfo |
| `Dbg` | FileVersionInfo |
| `Pre` | FileVersionInfo |
| `Priv` | FileVersionInfo |
| `Spec` | FileVersionInfo |
| `PropCt` | Property System |
| `Props` | Property System |
| `Intgr` | Win32 FSCTL |
| `Fs` | Win32 volume |
| `FsCap` | Win32 volume |

## Explicit unsupported claims

- Unsupported: exact locker DLL
- Unsupported: exact thumbnail COM object that rendered the current thumbnail
- Unsupported: treating Mark of the Web as a normal NTFS file attribute
- Unsupported: mutating object-ID creation from the inspector

## Tooltip rule

Use the tooltip text from the high-level spec verbatim. The short key is UI-stable and should not change without updating both specs.
