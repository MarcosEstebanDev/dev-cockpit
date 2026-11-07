using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using DevCockpit.Core.Ports;

namespace DevCockpit.Infrastructure.Windows;

/// <summary>
/// API keys en el Administrador de credenciales de Windows (destino <c>DevCockpit:&lt;clave&gt;</c>).
/// Se ven en Panel de control → Administrador de credenciales → Credenciales de Windows.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class CredentialManagerSecretStore : ISecretStore
{
    private const int CredTypeGeneric = 1;
    private const int CredPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;

    private static string Target(string key) => "DevCockpit:" + key;

    public string? Get(string key)
    {
        if (!CredRead(Target(key), CredTypeGeneric, 0, out var ptr))
        {
            var error = Marshal.GetLastWin32Error();
            return error == ErrorNotFound ? null : throw new Win32Exception(error);
        }
        try
        {
            var cred = Marshal.PtrToStructure<Credential>(ptr);
            if (cred.CredentialBlobSize == 0) return "";
            var bytes = new byte[cred.CredentialBlobSize];
            Marshal.Copy(cred.CredentialBlob, bytes, 0, bytes.Length);
            return Encoding.Unicode.GetString(bytes);
        }
        finally
        {
            CredFree(ptr);
        }
    }

    public void Set(string key, string value)
    {
        var bytes = Encoding.Unicode.GetBytes(value);
        var blob = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var cred = new Credential
            {
                Type = CredTypeGeneric,
                TargetName = Target(key),
                CredentialBlobSize = bytes.Length,
                CredentialBlob = blob,
                Persist = CredPersistLocalMachine,
                UserName = Environment.UserName,
            };
            if (!CredWrite(ref cred, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            Marshal.FreeHGlobal(blob);
        }
    }

    public void Delete(string key)
    {
        if (!CredDelete(Target(key), CredTypeGeneric, 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != ErrorNotFound) throw new Win32Exception(error);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public int Flags;
        public int Type;
        public string TargetName;
        public string? Comment;
        public long LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, int type, int flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref Credential credential, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}
