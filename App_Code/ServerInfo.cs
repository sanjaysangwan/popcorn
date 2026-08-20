using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.IO;
using System.Security.Principal;

/// <summary>
/// What Setup.aspx needs to tell you about the server itself. Shared hosting
/// gives you no console, so the site has to be able to answer "which providers
/// are installed", "am I 32-bit", and "who am I running as" on its own.
/// </summary>
public static class ServerInfo
{
    /// <summary>Jet 4.0 has no 64-bit build, so this decides whether .mdb is even possible.</summary>
    public static bool Is64BitProcess
    {
        get { return IntPtr.Size == 8; }
    }

    public static string Bitness
    {
        get { return Is64BitProcess ? "64-bit" : "32-bit"; }
    }

    /// <summary>
    /// The Windows account the site runs as - the one that needs write
    /// permission on App_Data.
    /// </summary>
    public static string ApplicationIdentity
    {
        get
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                return identity == null ? "(unknown)" : identity.Name;
            }
            catch (Exception ex)
            {
                return "(could not be read: " + ex.Message + ")";
            }
        }
    }

    public static bool AdoxAvailable
    {
        get
        {
            try { return Type.GetTypeFromProgID("ADOX.Catalog") != null; }
            catch { return false; }
        }
    }

    /// <summary>Every OLE DB provider registered for this process's bitness.</summary>
    public static List<string> OleDbProviders()
    {
        List<string> names = new List<string>();
        try
        {
            using (OleDbDataReader reader = OleDbEnumerator.GetRootEnumerator())
            {
                int column = -1;
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (String.Equals(reader.GetName(i), "SOURCES_NAME",
                                      StringComparison.OrdinalIgnoreCase))
                    {
                        column = i;
                        break;
                    }
                }

                while (reader.Read())
                {
                    if (column < 0 || reader.IsDBNull(column)) continue;
                    string name = Convert.ToString(reader.GetValue(column));
                    if (!String.IsNullOrEmpty(name) && !names.Contains(name)) names.Add(name);
                }
            }
            names.Sort();
        }
        catch (Exception ex)
        {
            names.Add("(could not be listed: " + ex.Message + ")");
        }
        return names;
    }

    public static bool HasProvider(string prefix)
    {
        foreach (string name in OleDbProviders())
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// Null when the folder can be written to, otherwise the reason it cannot.
    /// Access needs to create a lock file next to the database, so the folder
    /// has to be writable, not just the database file.
    /// </summary>
    public static string FolderWriteError(string folder)
    {
        if (String.IsNullOrEmpty(folder))
            return "The database folder could not be worked out from the connection string.";
        if (!Directory.Exists(folder))
            return "The folder " + folder + " does not exist.";

        string probe = Path.Combine(folder, "write-test-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return null;
        }
        catch (Exception ex)
        {
            return "The folder " + folder + " is not writable by " + ApplicationIdentity +
                   " (" + ex.Message + ").";
        }
    }

    /// <summary>Unwraps a COM/reflection exception into something readable.</summary>
    public static string Explain(Exception ex)
    {
        while (ex is System.Reflection.TargetInvocationException && ex.InnerException != null)
            ex = ex.InnerException;

        System.Runtime.InteropServices.COMException com =
            ex as System.Runtime.InteropServices.COMException;

        string text = ex.Message;
        if (com != null)
            text += " (HRESULT 0x" + com.ErrorCode.ToString("X8") + ")";

        return text;
    }
}
