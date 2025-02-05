﻿using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace VirusTotalContextMenu;

public static class UacHelper
{
    private const string UacRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
    private const string UacRegistryValue = "EnableLUA";

    private static uint STANDARD_RIGHTS_READ = 0x00020000;
    private static uint TOKEN_QUERY = 0x0008;
    private static uint TOKEN_READ = STANDARD_RIGHTS_READ | TOKEN_QUERY;

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr tokenHandle, TOKEN_INFORMATION_CLASS tokenInformationClass, IntPtr tokenInformation, uint tokenInformationLength, out uint returnLength);

    private enum TOKEN_INFORMATION_CLASS
    {
        TokenUser = 1,
        TokenGroups,
        TokenPrivileges,
        TokenOwner,
        TokenPrimaryGroup,
        TokenDefaultDacl,
        TokenSource,
        TokenType,
        TokenImpersonationLevel,
        TokenStatistics,
        TokenRestrictedSids,
        TokenSessionId,
        TokenGroupsAndPrivileges,
        TokenSessionReference,
        TokenSandBoxInert,
        TokenAuditPolicy,
        TokenOrigin,
        TokenElevationType,
        TokenLinkedToken,
        TokenElevation,
        TokenHasRestrictions,
        TokenAccessInformation,
        TokenVirtualizationAllowed,
        TokenVirtualizationEnabled,
        TokenIntegrityLevel,
        TokenUIAccess,
        TokenMandatoryPolicy,
        TokenLogonSid,
        MaxTokenInfoClass
    }

    private enum TOKEN_ELEVATION_TYPE
    {
        TokenElevationTypeDefault = 1,
        TokenElevationTypeFull,
        TokenElevationTypeLimited
    }

    public static bool IsUacEnabled
    {
        get
        {
            using var uacKey = Registry.LocalMachine.OpenSubKey(UacRegistryKey, false);

            var value = uacKey?.GetValue(UacRegistryValue);

            if (value == null)
                return false;

            return value.Equals(1);
        }
    }

    public static bool IsProcessElevated
    {
        get
        {
            if (IsUacEnabled)
            {
                if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_READ, out var tokenHandle))
                    throw new ApplicationException("Could not get process token.  Win32 Error Code: " + Marshal.GetLastWin32Error());

                var elevationResult = TOKEN_ELEVATION_TYPE.TokenElevationTypeDefault;

                var elevationResultSize = Marshal.SizeOf((int)elevationResult);
                var elevationTypePtr = Marshal.AllocHGlobal(elevationResultSize);

                var success = GetTokenInformation(tokenHandle, TOKEN_INFORMATION_CLASS.TokenElevationType, elevationTypePtr, (uint)elevationResultSize, out _);
                if (!success)
                    throw new ApplicationException("Unable to determine the current elevation.");

                elevationResult = (TOKEN_ELEVATION_TYPE)Marshal.ReadInt32(elevationTypePtr);
                return elevationResult == TOKEN_ELEVATION_TYPE.TokenElevationTypeFull;

            }

            using var currentUser = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(currentUser);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}