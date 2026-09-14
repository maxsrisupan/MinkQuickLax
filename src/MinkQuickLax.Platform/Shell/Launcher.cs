using System.Runtime.InteropServices;
using MinkQuickLax.Core.Model;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.Common;

namespace MinkQuickLax.Platform.Shell;

public enum LaunchError
{
    None,

    /// <summary>The file, folder or app is gone.</summary>
    NotFound,

    /// <summary>The user said no to the administrator prompt; not worth an error message.</summary>
    Cancelled,

    AccessDenied,

    /// <summary>No app is set to open this kind of file.</summary>
    NoAssociation,

    /// <summary>This kind of link cannot be opened by this version.</summary>
    Unsupported,

    Other,
}

public sealed record LaunchResult(LaunchError Error, int Win32Code = 0)
{
    public static LaunchResult Ok { get; } = new(LaunchError.None);

    public bool Succeeded => Error == LaunchError.None;
}

/// <summary>
/// Opens links through the Windows shell. Target and arguments are always passed separately, never
/// joined into a command line (PLAN.md 3.4). Call on the UI thread (the shell expects an STA thread).
/// </summary>
public static unsafe class Launcher
{
    private const string AppsFolderPrefix = @"shell:AppsFolder\";
    private const uint SeeMaskNoCloseProcess = 0x00000040;
    private const uint SeeMaskFlagNoUi = 0x00000400;
    private const uint SeeMaskUnicode = 0x00004000;

    public static bool CanRunAsAdmin(Link link) => link.Kind is LinkKind.App or LinkKind.Command;

    public static bool CanOpenLocation(Link link) => link.Kind is LinkKind.App or LinkKind.File or LinkKind.Folder;

    /// <summary>Whether <see cref="IsTargetMissing"/> can say anything about this kind.</summary>
    public static bool HasCheckableTarget(Link link) => link.Kind is LinkKind.App or LinkKind.File or LinkKind.Folder or LinkKind.Command;

    /// <summary>True when a file-system target does not exist. May touch the network for UNC paths; call off the UI thread.</summary>
    public static bool IsTargetMissing(Link link)
    {
        if (!HasCheckableTarget(link))
        {
            return false;
        }
        var path = Environment.ExpandEnvironmentVariables(link.Target);
        return link.Kind == LinkKind.Folder ? !Directory.Exists(path) : !File.Exists(path) && !Directory.Exists(path);
    }

    public static LaunchResult Open(Link link, bool asAdmin = false)
    {
        var (file, parameters) = link.Kind switch
        {
            LinkKind.ShellApp => (AppsFolderPrefix + link.Target, ""),
            LinkKind.App or LinkKind.File or LinkKind.Folder or LinkKind.Command => (Environment.ExpandEnvironmentVariables(link.Target), link.Arguments),
            LinkKind.Url or LinkKind.MsSettings => (link.Target, ""),
            _ => ("", ""),
        };
        if (file.Length == 0)
        {
            return new LaunchResult(LaunchError.Unsupported);
        }
        var directory = string.IsNullOrWhiteSpace(link.WorkingDirectory) ? null : Environment.ExpandEnvironmentVariables(link.WorkingDirectory);
        var verb = asAdmin || (link.RunAsAdmin && CanRunAsAdmin(link)) ? "runas" : null;
        return ShellExecute(file, parameters, directory, verb);
    }

    /// <summary>Opens Explorer at the item's folder with the item selected.</summary>
    public static LaunchResult OpenLocation(Link link)
    {
        if (!CanOpenLocation(link))
        {
            return new LaunchResult(LaunchError.Unsupported);
        }
        var path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(link.Target).TrimEnd('\\', '/'));
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return new LaunchResult(LaunchError.NotFound, (int)WIN32_ERROR.ERROR_FILE_NOT_FOUND);
        }

        ITEMIDLIST* pidl = null;
        fixed (char* p = path)
        {
            var hr = PInvoke.SHParseDisplayName(p, null, &pidl, 0, null);
            if (hr.Failed)
            {
                return FromHResult(hr);
            }
        }
        try
        {
            var hr = PInvoke.SHOpenFolderAndSelectItems(pidl, 0, null, 0);
            return hr.Succeeded ? LaunchResult.Ok : FromHResult(hr);
        }
        finally
        {
            PInvoke.ILFree(pidl);
        }
    }

    private static LaunchResult ShellExecute(string file, string parameters, string? directory, string? verb)
    {
        fixed (char* filePtr = file)
        fixed (char* parametersPtr = parameters)
        fixed (char* directoryPtr = directory)
        fixed (char* verbPtr = verb)
        {
            var info = new SHELLEXECUTEINFOW
            {
                cbSize = (uint)sizeof(SHELLEXECUTEINFOW),
                fMask = SeeMaskFlagNoUi | SeeMaskUnicode,
                lpVerb = verbPtr,
                lpFile = filePtr,
                lpParameters = parameters.Length == 0 ? null : parametersPtr,
                lpDirectory = directoryPtr,
                nShow = 1, // SW_SHOWNORMAL
            };
            if (PInvoke.ShellExecuteEx(ref info))
            {
                return LaunchResult.Ok;
            }
            return FromWin32(Marshal.GetLastPInvokeError());
        }
    }

    internal static LaunchResult FromWin32(int code) => new(code switch
    {
        (int)WIN32_ERROR.ERROR_FILE_NOT_FOUND or (int)WIN32_ERROR.ERROR_PATH_NOT_FOUND or (int)WIN32_ERROR.ERROR_BAD_NETPATH => LaunchError.NotFound,
        (int)WIN32_ERROR.ERROR_CANCELLED => LaunchError.Cancelled,
        (int)WIN32_ERROR.ERROR_ACCESS_DENIED or (int)WIN32_ERROR.ERROR_ELEVATION_REQUIRED => LaunchError.AccessDenied,
        (int)WIN32_ERROR.ERROR_NO_ASSOCIATION => LaunchError.NoAssociation,
        _ => LaunchError.Other,
    }, code);

    private static LaunchResult FromHResult(HRESULT hr) =>
        (hr.Value & 0xFFFF0000) == unchecked((int)0x80070000) ? FromWin32(hr.Value & 0xFFFF) : new LaunchResult(LaunchError.Other, hr.Value);
}
