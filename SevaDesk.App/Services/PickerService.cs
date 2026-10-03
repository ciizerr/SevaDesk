using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WinRT.Interop;

namespace SevaDesk_App.Services;

public class PickerService : IPickerService
{
    private static IntPtr _mainWindowHandle = IntPtr.Zero;

    public static void Initialize(IntPtr hwnd)
    {
        _mainWindowHandle = hwnd;
    }

    public Task<string?> PickFolderAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                var dialog = (IFileOpenDialog)new FileOpenDialog();
                dialog.SetOptions(FOS.FOS_PICKFOLDERS | FOS.FOS_FORCEFILESYSTEM);
                
                if (dialog.Show(_mainWindowHandle) == 0) // S_OK
                {
                    dialog.GetResult(out var item);
                    item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var path);
                    return path;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PickerService] PickFolder error: {ex.Message}");
            }
            return (string?)null;
        });
    }

    public Task<string?> PickFileAsync(string[] filters)
    {
        return Task.Run(() =>
        {
            try
            {
                var dialog = (IFileOpenDialog)new FileOpenDialog();
                dialog.SetOptions(FOS.FOS_FORCEFILESYSTEM | FOS.FOS_FILEMUSTEXIST);
                
                if (filters != null && filters.Length > 0)
                {
                    var filterString = string.Join(";", filters.Select(f => "*" + (f.StartsWith(".") ? f : "." + f)));
                    var filterSpec = new COMDLG_FILTERSPEC { pszName = "Files", pszSpec = filterString };
                    dialog.SetFileTypes(1, new[] { filterSpec });
                }

                if (dialog.Show(_mainWindowHandle) == 0)
                {
                    dialog.GetResult(out var item);
                    item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var path);
                    return path;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PickerService] PickFile error: {ex.Message}");
            }
            return (string?)null;
        });
    }

    public Task<IReadOnlyList<string>> PickMultipleFilesAsync(string[] filters)
    {
        return Task.Run(() =>
        {
            var results = new List<string>();
            try
            {
                var dialog = (IFileOpenDialog)new FileOpenDialog();
                dialog.SetOptions(FOS.FOS_FORCEFILESYSTEM | FOS.FOS_FILEMUSTEXIST | FOS.FOS_ALLOWMULTISELECT);
                
                if (filters != null && filters.Length > 0)
                {
                    var filterString = string.Join(";", filters.Select(f => "*" + (f.StartsWith(".") ? f : "." + f)));
                    var filterSpec = new COMDLG_FILTERSPEC { pszName = "Files", pszSpec = filterString };
                    dialog.SetFileTypes(1, new[] { filterSpec });
                }

                if (dialog.Show(_mainWindowHandle) == 0)
                {
                    dialog.GetResults(out var items);
                    items.GetCount(out var count);
                    for (uint i = 0; i < count; i++)
                    {
                        items.GetItemAt(i, out var item);
                        item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var path);
                        results.Add(path);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PickerService] PickMultipleFiles error: {ex.Message}");
            }
            return (IReadOnlyList<string>)results;
        });
    }

    public Task<string?> PickSaveFileAsync(string suggestedFileName, string extension, string fileTypeDescription)
    {
        return Task.Run(() =>
        {
            try
            {
                var dialog = (IFileSaveDialog)new FileSaveDialog();
                dialog.SetOptions(FOS.FOS_FORCEFILESYSTEM | FOS.FOS_PATHMUSTEXIST | FOS.FOS_OVERWRITEPROMPT);
                
                var ext = extension.StartsWith(".") ? extension : "." + extension;
                var filterSpec = new COMDLG_FILTERSPEC { pszName = fileTypeDescription, pszSpec = "*" + ext };
                dialog.SetFileTypes(1, new[] { filterSpec });
                dialog.SetDefaultExtension(ext);
                dialog.SetFileName(suggestedFileName);

                if (dialog.Show(_mainWindowHandle) == 0)
                {
                    dialog.GetResult(out var item);
                    item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var path);
                    return path;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PickerService] PickSaveFile error: {ex.Message}");
            }
            return (string?)null;
        });
    }

    // COM Interop Declarations
    [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
    private class FileOpenDialog { }

    [ComImport, Guid("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B")]
    private class FileSaveDialog { }

    [ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig] int Show([In] IntPtr parent);
        void SetFileTypes([In] uint cFileTypes, [In][MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);
        void SetFileTypeIndex([In] uint iFileType);
        void GetFileTypeIndex(out uint piFileType);
        void Advise([In, MarshalAs(UnmanagedType.Interface)] IntPtr pfde, out uint pdwCookie);
        void Unadvise([In] uint dwCookie);
        void SetOptions([In] FOS fos);
        void GetOptions(out FOS pfos);
        void SetDefaultFolder([In, MarshalAs(UnmanagedType.Interface)] IntPtr psi);
        void SetFolder([In, MarshalAs(UnmanagedType.Interface)] IntPtr psi);
        void GetFolder([MarshalAs(UnmanagedType.Interface)] out IntPtr ppsi);
        void GetCurrentSelection([MarshalAs(UnmanagedType.Interface)] out IntPtr ppsi);
        void SetFileName([In, MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([In, MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);
        void AddPlace([In, MarshalAs(UnmanagedType.Interface)] IntPtr psi, int fdap);
        void SetDefaultExtension([In, MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void Close([MarshalAs(UnmanagedType.Error)] int hr);
        void SetClientGuid([In] ref Guid guid);
        void ClearClientData();
        void SetFilter([MarshalAs(UnmanagedType.Interface)] IntPtr pFilter);
        void GetResults([MarshalAs(UnmanagedType.Interface)] out IShellItemArray ppenum);
        void GetSelectedItems([MarshalAs(UnmanagedType.Interface)] out IShellItemArray ppsai);
    }

    [ComImport, Guid("84bccd23-5fde-4cdb-aea4-af64b83d78ab"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileSaveDialog
    {
        [PreserveSig] int Show([In] IntPtr parent);
        void SetFileTypes([In] uint cFileTypes, [In][MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);
        void SetFileTypeIndex([In] uint iFileType);
        void GetFileTypeIndex(out uint piFileType);
        void Advise([In, MarshalAs(UnmanagedType.Interface)] IntPtr pfde, out uint pdwCookie);
        void Unadvise([In] uint dwCookie);
        void SetOptions([In] FOS fos);
        void GetOptions(out FOS pfos);
        void SetDefaultFolder([In, MarshalAs(UnmanagedType.Interface)] IntPtr psi);
        void SetFolder([In, MarshalAs(UnmanagedType.Interface)] IntPtr psi);
        void GetFolder([MarshalAs(UnmanagedType.Interface)] out IntPtr ppsi);
        void GetCurrentSelection([MarshalAs(UnmanagedType.Interface)] out IntPtr ppsi);
        void SetFileName([In, MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([In, MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);
        void AddPlace([In, MarshalAs(UnmanagedType.Interface)] IntPtr psi, int fdap);
        void SetDefaultExtension([In, MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void Close([MarshalAs(UnmanagedType.Error)] int hr);
        void SetClientGuid([In] ref Guid guid);
        void ClearClientData();
        void SetFilter([MarshalAs(UnmanagedType.Interface)] IntPtr pFilter);
        void SetSaveAsItem([In, MarshalAs(UnmanagedType.Interface)] IntPtr psi);
        void SetProperties([In, MarshalAs(UnmanagedType.Interface)] IntPtr pStore);
        void SetCollectedProperties([In, MarshalAs(UnmanagedType.Interface)] IntPtr pList, [In] int fAppendDefault);
        void GetProperties([MarshalAs(UnmanagedType.Interface)] out IntPtr ppStore);
        void ApplyProperties([In, MarshalAs(UnmanagedType.Interface)] IntPtr psi, [In, MarshalAs(UnmanagedType.Interface)] IntPtr pStore, [In] ref IntPtr hwnd, [In, MarshalAs(UnmanagedType.Interface)] IntPtr pSink);
    }

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler([In] IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName([In] SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes([In] uint sfgaoMask, out uint psfgaoAttribs);
        void Compare([In] IShellItem psi, [In] uint hint, out int piOrder);
    }

    [ComImport, Guid("b63ea76d-1f85-456f-a19c-48159efa858c"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemArray
    {
        void BindToHandler([In] IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppvOut);
        void GetPropertyStore([In] int flags, [In] ref Guid riid, out IntPtr ppvOut);
        void GetPropertyDescriptionList([In] ref Guid keyType, [In] ref Guid riid, out IntPtr ppvOut);
        void GetAttributes([In] int AttribFlags, [In] uint sfgaoMask, out uint psfgaoAttribs);
        void GetCount(out uint pdwNumItems);
        void GetItemAt([In] uint dwIndex, out IShellItem ppsi);
        void EnumItems(out IntPtr ppenumShellItems);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct COMDLG_FILTERSPEC
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string pszName;
        [MarshalAs(UnmanagedType.LPWStr)] public string pszSpec;
    }

    private enum SIGDN : uint
    {
        SIGDN_NORMALDISPLAY = 0x00000000,
        SIGDN_PARENTRELATIVEPARSING = 0x80018001,
        SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000,
        SIGDN_PARENTRELATIVEEDITING = 0x80031001,
        SIGDN_DESKTOPABSOLUTEEDITING = 0x8004c000,
        SIGDN_FILESYSPATH = 0x80058000,
        SIGDN_URL = 0x80068000,
        SIGDN_PARENTRELATIVEFORADDRESSBAR = 0x8007c001,
        SIGDN_PARENTRELATIVE = 0x80080001
    }

    [Flags]
    private enum FOS : uint
    {
        FOS_OVERWRITEPROMPT = 0x00000002,
        FOS_STRICTFILETYPES = 0x00000004,
        FOS_NOCHANGEDIR = 0x00000008,
        FOS_PICKFOLDERS = 0x00000020,
        FOS_FORCEFILESYSTEM = 0x00000040,
        FOS_ALLNONSTORAGEITEMS = 0x00000080,
        FOS_NOVALIDATE = 0x00000100,
        FOS_ALLOWMULTISELECT = 0x00000200,
        FOS_PATHMUSTEXIST = 0x00000800,
        FOS_FILEMUSTEXIST = 0x00001000,
        FOS_CREATEPROMPT = 0x00002000,
        FOS_SHAREAWARE = 0x00004000,
        FOS_NOREADONLYRETURN = 0x00008000,
        FOS_NOTESTFILECREATE = 0x00010000,
        FOS_HIDEMRUPLACES = 0x00020000,
        FOS_HIDEPINNEDPLACES = 0x00040000,
        FOS_NODEREFERENCELINKS = 0x00100000,
        FOS_DONTADDTORECENT = 0x02000000,
        FOS_FORCESHOWHIDDEN = 0x10000000,
        FOS_DEFAULTNOMINIMODE = 0x20000000,
        FOS_FORCEPREVIEWPANEON = 0x40000000
    }
}
