using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Boxes.App.Services;

internal static class ShellLinkHelper
{
    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLinkCoClass
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig]
        int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    private interface IPropertyStore
    {
        void GetCount(out uint cProps);
        void GetAt(uint iProp, out PROPERTYKEY pkey);
        void GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        void SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
        void Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PROPVARIANT
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr pointerValue;
    }

    private const ushort VT_LPWSTR = 31;

    private static readonly PROPERTYKEY PKEY_AppUserModel_ID = new()
    {
        fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        pid = 5
    };

    private static readonly PROPERTYKEY PKEY_AppUserModel_RelaunchCommand = new()
    {
        fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        pid = 2
    };

    private static readonly PROPERTYKEY PKEY_AppUserModel_RelaunchDisplayNameResource = new()
    {
        fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        pid = 4
    };

    private static readonly PROPERTYKEY PKEY_AppUserModel_RelaunchIconResource = new()
    {
        fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        pid = 3
    };

    public static bool CreateShortcut(string linkPath, string targetPath, string? arguments = null, string? workingDir = null, string? description = null, string? iconPath = null, int iconIndex = 0, string? appUserModelId = null, string? relaunchDisplayName = null)
    {
        try
        {
            var shellLink = (IShellLinkW)new ShellLinkCoClass();
            shellLink.SetPath(targetPath);
            shellLink.SetArguments(arguments ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(workingDir))
            {
                shellLink.SetWorkingDirectory(workingDir);
            }
            if (!string.IsNullOrWhiteSpace(description))
            {
                shellLink.SetDescription(description);
            }
            if (!string.IsNullOrWhiteSpace(iconPath))
            {
                shellLink.SetIconLocation(iconPath, iconIndex);
            }

            var persist = (IPersistFile)shellLink;
            persist.Save(linkPath, true);

            // Set AppUserModelID and relaunch metadata to ensure a distinct taskbar identity
            if (!string.IsNullOrWhiteSpace(appUserModelId))
            {
                try
                {
                    var propStore = (IPropertyStore)shellLink;

                    SetStringPropertyReadonly(propStore, PKEY_AppUserModel_ID, appUserModelId);

                    var relaunchCmd = $"\"{targetPath}\" {arguments}".Trim();
                    SetStringPropertyReadonly(propStore, PKEY_AppUserModel_RelaunchCommand, relaunchCmd);

                    if (!string.IsNullOrWhiteSpace(relaunchDisplayName))
                    {
                        SetStringPropertyReadonly(propStore, PKEY_AppUserModel_RelaunchDisplayNameResource, relaunchDisplayName);
                    }

                    if (!string.IsNullOrWhiteSpace(iconPath))
                    {
                        // Icon resource string as `path,iconIndex`
                        var iconRes = iconIndex != 0 ? $"{iconPath},{iconIndex}" : iconPath;
                        SetStringPropertyReadonly(propStore, PKEY_AppUserModel_RelaunchIconResource, iconRes);
                    }

                    propStore.Commit();
                    // Save again to persist property store changes
                    persist.Save(linkPath, true);
                }
                catch
                {
                    // Ignore property store failures on older OSes
                }
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void SetStringProperty(IPropertyStore store, ref PROPERTYKEY key, string value)
    {
        var pv = new PROPVARIANT
        {
            vt = VT_LPWSTR,
            pointerValue = Marshal.StringToCoTaskMemUni(value ?? string.Empty)
        };
        try
        {
            store.SetValue(ref key, ref pv);
        }
        finally
        {
            if (pv.pointerValue != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(pv.pointerValue);
            }
        }
    }

    private static void SetStringPropertyReadonly(IPropertyStore store, PROPERTYKEY key, string value)
    {
        var k = key; // local copy allows passing by ref
        SetStringProperty(store, ref k, value);
    }

    #region Jump List Support

    [ComImport]
    [Guid("6332DEBF-87B5-4670-90C0-5E57B408A49E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICustomDestinationList
    {
        void SetAppID([MarshalAs(UnmanagedType.LPWStr)] string pszAppID);
        void BeginList(out uint pcMinSlots, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
        void AppendCategory([MarshalAs(UnmanagedType.LPWStr)] string pszCategory, IObjectArray poa);
        void AppendKnownCategory(int category);
        void AddUserTasks(IObjectArray poa);
        void CommitList();
        void GetRemovedDestinations([MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
        void DeleteList([MarshalAs(UnmanagedType.LPWStr)] string pszAppID);
        void AbortList();
    }

    [ComImport]
    [Guid("77F10CF0-3DB5-4966-B520-B7C54FD35ED6")]
    private class DestinationListCoClass
    {
    }

    [ComImport]
    [Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IObjectArray
    {
        void GetCount(out uint pcObjects);
        void GetAt(uint uiIndex, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
    }

    [ComImport]
    [Guid("5632B1A4-E38A-400A-928A-D4CD63230295")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IObjectCollection : IObjectArray
    {
        new void GetCount(out uint pcObjects);
        new void GetAt(uint uiIndex, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
        void AddObject([MarshalAs(UnmanagedType.IUnknown)] object punk);
        void AddFromArray(IObjectArray poaSource);
        void RemoveObjectAt(uint uiIndex);
        void Clear();
    }

    [ComImport]
    [Guid("2D3468C1-36A7-43B6-AC24-D3F02FD9607A")]
    private class EnumerableObjectCollectionCoClass
    {
    }

    private static readonly Guid IID_IObjectArray = new("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9");

    public static bool SetJumpListTasks(string appUserModelId, params (string title, string exePath, string arguments, string? iconPath, int iconIndex)[] tasks)
    {
        try
        {
            var destList = (ICustomDestinationList)new DestinationListCoClass();
            destList.SetAppID(appUserModelId);
            destList.BeginList(out _, IID_IObjectArray, out _);

            var collection = (IObjectCollection)new EnumerableObjectCollectionCoClass();

            foreach (var (title, exePath, arguments, iconPath, iconIndex) in tasks)
            {
                var link = (IShellLinkW)new ShellLinkCoClass();
                link.SetPath(exePath);
                link.SetArguments(arguments);
                link.SetDescription(title);
                if (!string.IsNullOrWhiteSpace(iconPath))
                {
                    link.SetIconLocation(iconPath, iconIndex);
                }

                // Set the title via property store
                try
                {
                    var propStore = (IPropertyStore)link;
                    SetStringPropertyReadonly(propStore, PKEY_Title, title);
                    propStore.Commit();
                }
                catch
                {
                    // Ignore property store failures
                }

                collection.AddObject(link);
            }

            destList.AddUserTasks((IObjectArray)collection);
            destList.CommitList();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static readonly PROPERTYKEY PKEY_Title = new()
    {
        fmtid = new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"),
        pid = 2
    };

    #endregion
}


