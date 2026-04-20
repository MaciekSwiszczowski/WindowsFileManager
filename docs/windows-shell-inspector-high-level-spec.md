# Windows Shell Replacement Inspector – High-level Spec

## Scope

This spec defines the **file/folder inspector panel** for a Windows 11 desktop application that targets **NTFS-first diagnostics** and is used by Windows engineers and testers. The inspector is intentionally broader than Explorer's details pane.

The inspector must cover three layers:

1. **Curated core fields** that are always or usually useful.
2. **Advanced diagnostics** that are expensive, niche, or only apply on some items.
3. A **raw Property System dump** so the app remains useful when a third-party property handler, sync provider, shell extension, or future Windows build exposes extra properties.

## Design principles

- Use **WinRT / Windows Storage** for top-level properties, basic properties, thumbnails, provider info, and Property System access.
- Use **managed .NET** for cheap file-system basics.
- Use **Win32 handle-based calls** for NTFS identity, reparse data, streams, lock diagnostics, and other Explorer-grade details.
- Keep the UI simple: **short key + value**, with a **long tooltip** for every key.
- Treat the inspector as **best effort**: unsupported or inaccessible fields should display `N/A` plus the reason, not fail the whole panel.
- Load expensive categories lazily and cache them per selection.

## Important constraints

- There is **no closed, finite property list** for Windows items. The Windows Property System is extensible through property handlers and other shell integrations. The inspector therefore must include a raw `System.*` property dump via `RetrievePropertiesAsync(null)`.
- `Locked by DLL` is **not** a supported public diagnostic surface. The supported result is **which process / service / app** is using the item. Do not claim a specific DLL is the locker unless a future supported API exposes that directly.
- `Custom thumbnail provider` can be detected only as **registration / association data**. The app can show the registered handler CLSID(s), but that is not proof of the exact COM object that rendered the current thumbnail.
- `Downloaded from internet` / `Blocked` is **not** a normal NTFS file attribute. Treat it as **Mark of the Web / zone-of-origin state**, typically backed by the `Zone.Identifier` alternate data stream and Attachment Manager policy.

## Recommended sections

1. Identity
2. Times and size
3. Flags
4. NTFS
5. IDs
6. Links / reparse
7. Streams
8. Trust / origin
9. Security
10. In-use
11. Cloud
12. Thumbnails / associations
13. PE version
14. Raw property bag
15. Optional ReFS / volume context

The inspector UI groups deferred details under `NTFS`, `IDs`, `Locks`, `Links`, `Streams`, `Security`, `Thumbnails`, and `Cloud`.
`NTFS` contains cheap immediate flags and a deferred live-metadata refresh for native NTFS timestamps and refreshed attribute bits.
`Cloud` is the final deferred category and should remain hidden unless the selected item belongs to a sync root or exposes cloud/provider state.

## Field catalog

| Section | Key | Applies | Tooltip text | Source | Load policy |
|---|---|---|---|---|---|
| Identity | Name | F/D | Leaf file or folder name only. | WinRT top-level / .NET FileSystemInfo | Always |
| Identity | Disp | F/D | Display name shown to the user. May differ from the raw leaf name for some shell-backed items. | WinRT top-level | Always |
| Identity | Type | F/D | User-facing type text, such as 'Text Document' or 'File folder'. | WinRT top-level / Property System | Always |
| Identity | CType | F | Declared content type / MIME-like type if available. | WinRT top-level | Default for files |
| Identity | Ext | F | File name extension including the leading dot. | .NET FileInfo | Default for files |
| Identity | Path | F/D | Original full path selected in the UI. | .NET / WinRT | Always |
| Identity | FPath | F/D | Final normalized path resolved from an open handle. Use to expose path changes caused by junctions, symlinks, mount points, or case normalization. | GetFinalPathNameByHandleW | Lazy |
| Identity | Exist | F/D | Whether the selected path currently exists at refresh time. | .NET FileSystemInfo | Always |
| Identity | Parent | F/D | Parent directory path. | .NET FileSystemInfo / WinRT parent | Default |
| Identity | Kind | F/D | Simple kind flag: file or directory. | .NET / WinRT | Always |
| Times/Size | CTime | F/D | Creation timestamp as stored by the file system. | .NET FileSystemInfo | Always |
| Times/Size | MTime | F/D | Last write / modification timestamp. | .NET FileSystemInfo + WinRT BasicProperties | Always |
| Times/Size | ATime | F/D | Last access timestamp. | .NET FileSystemInfo | Default |
| Times/Size | IDate | F/D | Shell 'item date'. Useful when Explorer surfaces a date that is not simply last write time. | WinRT BasicProperties | Default |
| Times/Size | Size | F | Logical file size in bytes. | .NET FileInfo / WinRT BasicProperties | Always for files |
| Times/Size | CSize | F | Bytes actually allocated on disk for a compressed or sparse file. For an uncompressed regular file this is usually close to allocation size, not logical size. | GetCompressedFileSizeW | Lazy for files |
| Flags | Attr | F/D | Raw combined file attribute flags. | .NET / Win32 | Always |
| NTFS | RO | F/D | Read-only attribute. | .NET / Win32 | Always |
| NTFS | Hid | F/D | Hidden attribute. | .NET / Win32 | Always |
| NTFS | Sys | F/D | System attribute. | .NET / Win32 | Always |
| NTFS | Arc | F/D | Archive attribute. | .NET / Win32 | Default |
| NTFS | Tmp | F/D | Temporary attribute. | .NET / Win32 | Default |
| NTFS | Offl | F/D | Offline attribute. Often present for cloud placeholders or remote storage scenarios. | Win32 attributes | Default |
| NTFS | NIdx | F/D | Not-content-indexed attribute. | Win32 attributes | Default |
| NTFS | Efs | F/D | Encrypted with EFS. | Win32 attributes | Default |
| NTFS | Cmp | F/D | NTFS compression attribute. | Win32 attributes | Default |
| NTFS | Sprs | F/D | Sparse file attribute. | Win32 attributes | Default |
| NTFS | RPt | F/D | Reparse-point attribute. | Win32 attributes | Always |
| Flags | Pin | F/D | Pinned intent bit for cloud files. | Win32 attributes / CfApi | Default |
| Flags | Unpin | F/D | Unpinned intent bit for cloud files. | Win32 attributes / CfApi | Default |
| Flags | RcOpen | F/D | Cloud recall-on-open bit. | Win32 attributes / CfApi | Default |
| Flags | RcData | F/D | Cloud recall-on-data-access bit. | Win32 attributes / CfApi | Default |
| IDs | FileId | F/D | 128-bit stable file or directory identifier on the volume. | GetFileInformationByHandleEx(FileIdInfo) | Always |
| IDs | VolSN | F/D | Volume serial number paired with FileId. Use both together for uniqueness on the machine. | FILE_ID_INFO | Always |
| IDs | Id64 | F/D | Legacy 64-bit file index exposed by BY_HANDLE_FILE_INFORMATION. Keep only for diagnostics and comparison with older tools. | GetFileInformationByHandle | Default |
| IDs | Links | F/D | Hard-link count. | GetFileInformationByHandle | Default |
| Links/Reparse | LnTgt | F/D | Resolved target path for a symbolic link or junction. For .lnk shortcuts also show Shell target path if available from the property system. | .NET LinkTarget + System.Link.TargetParsingPath | Default when applicable |
| Links/Reparse | LnkSt | F | Shell-link resolution status for .lnk items. | System.Link.Status | Default when applicable |
| Links/Reparse | RTag | F/D | Raw reparse tag. | FILE_ATTRIBUTE_TAG_INFO | Default when applicable |
| Links/Reparse | RData | F/D | Raw reparse payload, displayed as decoded summary or hex blob for diagnostics. | FSCTL_GET_REPARSE_POINT | Advanced |
| Links/Reparse | ObjId | F/D | NTFS object identifier, if present. | FSCTL_GET_OBJECT_ID | Advanced |
| Streams | ADS | F/D | Whether named alternate data streams exist in addition to the default stream. | FindFirstStreamW / FindNextStreamW | Default |
| Streams | ADSCnt | F/D | Count of named alternate data streams, excluding the unnamed default data stream. | FindFirstStreamW / FindNextStreamW | Default |
| Streams | ADSList | F/D | List of stream names and sizes. | FindFirstStreamW / FindNextStreamW | Lazy |
| Trust/Origin | Blk | F | Whether Windows marked the file with Mark of the Web / zone-of-origin data, typically because it was downloaded from the internet or saved from email. This explains security warnings, Office macro blocking, PowerShell RemoteSigned behavior, and some Explorer preview behavior. | Zone.Identifier ADS / Attachment Manager | Default for files |
| Trust/Origin | Zone | F | Zone identifier stored in Mark of the Web. Surface both the numeric ZoneId and a friendly label when known. | Zone.Identifier ADS | Default when applicable |
| Trust/Origin | ZAds | F | Whether the special `Zone.Identifier` alternate data stream exists. | FindFirstStreamW / CreateFileW on stream path | Default for files |
| Trust/Origin | ZTxt | F | Raw text contents of the `Zone.Identifier` stream for diagnostics. | CreateFileW on stream path | Advanced for files |
| Security | Owner | F/D | Owner SID/account from the security descriptor. | .NET ACL APIs | Default |
| Security | Group | F/D | Primary group from the security descriptor when present. | .NET ACL APIs | Default |
| Security | DACL | F/D | Canonicalized DACL summary, including whether explicit entries exist and how many allow/deny ACEs are present. | .NET ACL APIs | Default |
| Security | SACL | F/D | Audit-rule summary if readable with current privileges. | .NET ACL APIs | Lazy |
| Security | Inh | F/D | Whether access rules are inherited. | .NET ACL APIs | Default |
| Security | Prot | F/D | Whether inheritance is protected / disabled. | .NET ACL APIs | Default |
| In-use | InUse | F/D | Whether the item is currently reported as being used by one or more processes or services. | Restart Manager | Default |
| In-use | LockBy | F/D | List of locking applications and/or services. | Restart Manager / IFileIsInUse | Lazy |
| In-use | LockPid | F/D | PIDs of processes reported by Restart Manager. | Restart Manager | Lazy |
| In-use | LockSvc | F/D | Service short names reported by Restart Manager. | Restart Manager | Lazy |
| In-use | Usage | F/D | Usage type from IFileIsInUse when available. | IFileIsInUse | Advanced |
| In-use | CanSw | F/D | Whether the UI can switch to the locking application's window. | IFileIsInUse | Advanced |
| In-use | CanCls | F/D | Whether the locking application can be politely asked to close the file. | IFileIsInUse | Advanced |
| Cloud | Prov | F/D | Cloud or storage provider display info for the item. | WinRT Provider | Default when applicable |
| Cloud | SyncRt | F/D | Sync root path or display identity that owns the item. | StorageProviderSyncRootManager | Default when applicable |
| Cloud | SyncId | F/D | Sync root registration identifier. | StorageProviderSyncRootInfo | Default when applicable |
| Cloud | ProvId | F/D | Provider identifier from the sync-root registration. | StorageProviderSyncRootInfo | Default when applicable |
| Cloud | Avail | F | Whether the file is available locally, cached locally, or can currently be downloaded. | StorageFile.IsAvailable | Default for files |
| Cloud | CfState | F/D | Placeholder state bitmask interpreted into readable badges such as placeholder, in-sync, partially on disk, or fully present. | CfGetPlaceholderStateFromFileInfo | Default when applicable |
| Cloud | CfInfo | F/D | Detailed placeholder metadata from CfApi when the item is a cloud placeholder. | CfGetPlaceholderInfo | Advanced |
| Cloud | CusSt | F/D | Provider-defined custom item states shown in Explorer, including value text and icon resource if present. | StorageProviderItemProperty / Property System | Default when applicable |
| Thumbnail/Assoc | Thumb | F/D | Rendered thumbnail image, if any. | WinRT GetThumbnailAsync | Default |
| Thumbnail/Assoc | ProgId | F | Resolved ProgID / association identity for the file extension. | AssocQueryString | Default for files |
| Thumbnail/Assoc | ThCls | F | Registered thumbnail-handler CLSID(s) for the file type. This is registration data, not proof of the exact COM object that generated the currently shown thumbnail. | AssocQueryString / SHAssocEnumHandlers / registry | Default for files |
| Thumbnail/Assoc | PrCls | F | Registered preview-handler CLSID(s) for the file type. | Association APIs / registry | Default for files |
| Thumbnail/Assoc | PhCls | F | Registered property-handler CLSID(s) for the file type. | Association APIs / registry | Default for files |
| Thumbnail/Assoc | OvCls | F/D | Registered icon-overlay handler CLSID(s) that may affect Explorer presentation. | Association APIs / registry | Advanced |
| Thumbnail/Assoc | PsCls | F | Registered property-sheet handler CLSID(s) for the file type. | Association APIs / registry | Advanced |
| PE Version | Co | F | Company name from Win32 version resources. | FileVersionInfo | Default for PE files |
| PE Version | Prod | F | Product name from version resources. | FileVersionInfo | Default for PE files |
| PE Version | FVer | F | File version from version resources. | FileVersionInfo | Default for PE files |
| PE Version | PVer | F | Product version from version resources. | FileVersionInfo | Default for PE files |
| PE Version | Orig | F | Original file name from version resources. | FileVersionInfo | Default for PE files |
| PE Version | Int | F | Internal name from version resources. | FileVersionInfo | Default for PE files |
| PE Version | Dbg | F | Debug build flag from version resources. | FileVersionInfo | Advanced for PE files |
| PE Version | Pre | F | Prerelease flag from version resources. | FileVersionInfo | Advanced for PE files |
| PE Version | Priv | F | Private build flag from version resources. | FileVersionInfo | Advanced for PE files |
| PE Version | Spec | F | Special build flag from version resources. | FileVersionInfo | Advanced for PE files |
| Raw property bag | PropCt | F/D | Count of raw `System.*` properties returned by the Property System for this machine and this item. | RetrievePropertiesAsync(null) | Advanced |
| Raw property bag | Props | F/D | Full dump of raw `System.*` property key/value pairs. This is the extensibility escape hatch for handler-specific, provider-specific, and future Windows properties. | RetrievePropertiesAsync(null) | Advanced |
| Optional ReFS | Intgr | F/D | Integrity-stream setting and checksum algorithm on ReFS, if supported. | FSCTL_GET_INTEGRITY_INFORMATION | Advanced / only where supported |
| Volume | Fs | F/D | File-system name for the hosting volume, e.g. NTFS. | GetVolumeInformation | Default |
| Volume | FsCap | F/D | Relevant file-system capabilities of the hosting volume, such as ACLs, EFS, compression, sparse files, reparse points, object IDs, and named streams support. | GetVolumeInformation | Default |

## UX rules

- The short key should be visible in the main inspector row.
- The long explanation belongs in the tooltip and should be phrased in plain user-facing language. Do not expose internal API, COM, or interface names.
- Multi-value diagnostics such as `LockBy`, `ADSList`, `OvCls`, and raw properties may render as a multi-line value cell.
- `Thumb` should render as an image row; all other fields remain key-value rows.
- When the inspector UI is grouped by category, each category section should persist across updates. Selecting a new item should update the existing category contents, not recreate the whole grouped UI.
- The row model inside each category should expose an explicit `IsVisible` state. Existing row objects must stay alive across selections; only row values and `IsVisible` change.
- Category row membership should also be stable. Create each category's row list once, keep those row references alive, and let the view hide rows by `IsVisible` instead of rebuilding category row collections during refresh.
- When the same item is refreshed, already-visible deferred rows should stay visible and show a loading indicator instead of disappearing. Final row visibility may change only after the deferred load completes.
- In the grouped UI, each category section should render its rows as a simple two-column grid with a fixed `Property` column and a flexible `Value` column. The `Value` side must always take the remaining available inspector width.
- When grouped categories live inside a `ScrollViewer`, the grouped host must own the finite width. Do not depend on nested `ItemsControl` presenters to stretch each category; prefer a finite-width host with repeater-style layout so the `Value` column actually receives the remaining width.
- If WinUI layout still measures grouped categories to content, the control may publish the measured available width into the grouped category view models and bind category width explicitly. Correct width is more important than keeping the layout purely declarative.
- Expensive categories should not block first paint. Show them progressively.
- The `NTFS` category should expose all four NTFS timestamps together at the top of the category: creation, last access, last write, and MFT change time.
- The `MFT Changed` value comes from NTFS metadata (`FILE_BASIC_INFO.ChangeTime`), not from the managed `FileSystemInfo` snapshot.
- For the small subset of binary NTFS flags that are safe to change through normal file attributes, the value cell may include an inline toggle control next to the `Yes` / `No` text.
- Do not duplicate creation / last-write timestamps in both `Basic` and `NTFS`. `NTFS` owns the timestamp set.
- Property labels should stay short. Use tooltips for the longer explanation.
- Every row should support copy-to-clipboard of the value.
- Unsupported selections such as multiselection or the synthetic `..` parent row should hide inspector content instead of showing partial or stale state.
- Shell-facing actions such as opening the Windows Properties dialog must use the normal display path, not the internal long-path `\\?\` representation.

## Recommended loading policy

### Immediate
- Identity
- Times and size except `CSize`
- Flags
- `FileId`, `VolSN`
- `Fs`, `FsCap`

### Background
- `FPath`
- `Id64`, `Links`
- Reparse / link fields
- Streams
- Trust / origin summary
- Security summary
- Cloud summary
- Thumbnail
- PE version

### On-demand / Advanced
- Raw reparse payload
- Full lock list
- SACL summary
- Full raw property bag
- Object ID
- ReFS integrity

## High-level documentation

- [Files, folders, and libraries](https://learn.microsoft.com/en-us/windows/apps/develop/files/)
- [Get file properties](https://learn.microsoft.com/en-us/windows/apps/develop/files/file-properties)
- [Property System Overview](https://learn.microsoft.com/en-us/windows/win32/properties/property-system-overview)
- [Windows Property System](https://learn.microsoft.com/en-us/windows/win32/properties/windows-properties-system)
- [Creating and opening files](https://learn.microsoft.com/en-us/windows/win32/fileio/creating-and-opening-files)
- [Obtaining a handle to a directory](https://learn.microsoft.com/en-us/windows/win32/fileio/obtaining-a-handle-to-a-directory)
- [File attribute constants](https://learn.microsoft.com/en-us/windows/win32/fileio/file-attribute-constants)
- [Reparse points](https://learn.microsoft.com/en-us/windows/win32/fileio/reparse-points)
- [Reparse points and file operations](https://learn.microsoft.com/en-us/windows/win32/fileio/reparse-points-and-file-operations)
- [File streams](https://learn.microsoft.com/en-us/windows/win32/fileio/file-streams)
- [Obtaining volume information](https://learn.microsoft.com/en-us/windows/win32/fileio/obtaining-volume-information)
- [Attachment Manager in Microsoft Windows](https://support.microsoft.com/en-us/topic/information-about-the-attachment-manager-in-microsoft-windows-c48a4dcd-8de5-2af5-ee9b-cd795ae42738)
- [Policy CSP - AttachmentManager](https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-attachmentmanager)
- [File Explorer disables preview for files downloaded from the internet](https://support.microsoft.com/en-us/topic/file-explorer-automatically-disables-the-preview-feature-for-files-downloaded-from-the-internet-56d55920-6187-4aae-a4f6-102454ef61fb)
- [Access control model](https://learn.microsoft.com/en-us/windows/win32/secauthz/access-control-model)
- [.NET ACL overview for files/directories](https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-add-or-remove-access-control-list-entries)
- [Restart Manager](https://learn.microsoft.com/en-us/windows/win32/rstmgr/restart-manager-portal)
- [About Restart Manager](https://learn.microsoft.com/en-us/windows/win32/rstmgr/about-restart-manager)
- [Cloud Sync Engines](https://learn.microsoft.com/en-us/windows/win32/cfapi/cloud-files-api-portal)
- [Build a Cloud Sync Engine that Supports Placeholder Files](https://learn.microsoft.com/en-us/windows/win32/cfapi/build-a-cloud-file-sync-engine)
- [Determining availability of Microsoft OneDrive files](https://learn.microsoft.com/en-us/windows/apps/develop/files/determine-availability-microsoft-onedrive-files)
- [File types and file associations](https://learn.microsoft.com/en-us/windows/win32/shell/fa-intro)
- [How file associations work](https://learn.microsoft.com/en-us/windows/win32/shell/fa-how-work)
- [Programmatic Identifiers (ProgID)](https://learn.microsoft.com/en-us/windows/win32/shell/fa-progids)
- [Thumbnails and icons](https://learn.microsoft.com/en-us/windows/win32/shell/thumbnails-and-icons-bumper)
- [Thumbnail handlers](https://learn.microsoft.com/en-us/windows/win32/shell/thumbnail-providers)
- [Preview handlers and shell preview host](https://learn.microsoft.com/en-us/windows/win32/shell/preview-handlers)
- [Implementing property handlers](https://learn.microsoft.com/en-us/windows/win32/properties/building-property-handlers)
- [Understanding property handlers](https://learn.microsoft.com/en-us/windows/win32/properties/building-property-handlers-properties)
- [Property sheet handlers](https://learn.microsoft.com/en-us/windows/win32/shell/propsheet-handlers)
- [Shell links](https://learn.microsoft.com/en-us/windows/win32/shell/links)
- [Distributed link tracking and object identifiers](https://learn.microsoft.com/en-us/windows/win32/fileio/distributed-link-tracking-and-object-identifiers)
- [ReFS overview](https://learn.microsoft.com/en-us/windows-server/storage/refs/refs-overview)
- [Maximum path length limitation](https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation)

## Non-goals

- No media metadata sections (`music`, `video`, `image`, `document`).
- No unsupported private handle-table scanning.
- No writes, repairs, or mutating FSCTLs from the inspector.

## Implementation note

The companion implementation spec is intentionally more mechanical. It lists the exact APIs to call for each category and is the source of truth for bugfixing and agent work.
