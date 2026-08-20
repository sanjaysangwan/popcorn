using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.OleDb;

/// <summary>
/// Thin data-access helper over the Microsoft Access database.
///
/// Everything goes through parameterised OleDb commands. OleDb parameters are
/// positional, so the order of the values passed to these methods must match
/// the order the "?" placeholders appear in the SQL text.
/// </summary>
public static class Db
{
    public static string ConnectionString
    {
        get
        {
            ConnectionStringSettings cs = ConfigurationManager.ConnectionStrings["FamilyMovies"];
            if (cs == null || String.IsNullOrEmpty(cs.ConnectionString))
                throw new ConfigurationErrorsException(
                    "The 'FamilyMovies' connection string is missing from web.config.");

            // |DataDirectory| is only expanded automatically by some providers,
            // so expand it ourselves to keep Jet and ACE behaving the same way.
            string value = cs.ConnectionString;
            object dataDir = AppDomain.CurrentDomain.GetData("DataDirectory");
            if (dataDir != null)
                value = value.Replace("|DataDirectory|", dataDir.ToString().TrimEnd('\\') + "\\");
            return value;
        }
    }

    public static OleDbConnection Open()
    {
        OleDbConnection cn = new OleDbConnection(ConnectionString);
        cn.Open();
        return cn;
    }

    /// <summary>Access TEXT columns stop at 255 characters; longer values are MEMO.</summary>
    private const int TextColumnLimit = 255;

    private static void Bind(OleDbCommand cmd, object[] args)
    {
        if (args == null) return;

        for (int i = 0; i < args.Length; i++)
        {
            object v = args[i];
            string name = "p" + i;

            if (v == null) v = DBNull.Value;
            else if (v is bool) v = ((bool)v) ? -1 : 0;   // Jet stores YESNO as -1/0

            string text = v as string;
            if (text != null && text.Length > TextColumnLimit)
            {
                // Left to infer the type, OleDb sends a long string as a sized
                // VarWChar and Jet answers "the field is too small to accept the
                // amount of data". Saying LongVarWChar up front is the fix.
                OleDbParameter parameter = new OleDbParameter(name, OleDbType.LongVarWChar);
                parameter.Value = text;
                cmd.Parameters.Add(parameter);
            }
            else
            {
                cmd.Parameters.AddWithValue(name, v);
            }
        }
    }

    public static int Execute(string sql, params object[] args)
    {
        using (OleDbConnection cn = Open())
        using (OleDbCommand cmd = new OleDbCommand(sql, cn))
        {
            Bind(cmd, args);
            return cmd.ExecuteNonQuery();
        }
    }

    public static object Scalar(string sql, params object[] args)
    {
        using (OleDbConnection cn = Open())
        using (OleDbCommand cmd = new OleDbCommand(sql, cn))
        {
            Bind(cmd, args);
            object o = cmd.ExecuteScalar();
            return (o == DBNull.Value) ? null : o;
        }
    }

    /// <summary>Runs an INSERT and returns the new AutoNumber key.</summary>
    public static int Insert(string sql, params object[] args)
    {
        using (OleDbConnection cn = Open())
        {
            using (OleDbCommand cmd = new OleDbCommand(sql, cn))
            {
                Bind(cmd, args);
                cmd.ExecuteNonQuery();
            }
            // @@IDENTITY must be read on the same open connection.
            using (OleDbCommand id = new OleDbCommand("SELECT @@IDENTITY", cn))
            {
                return Convert.ToInt32(id.ExecuteScalar());
            }
        }
    }

    public static DataTable Query(string sql, params object[] args)
    {
        using (OleDbConnection cn = Open())
        using (OleDbCommand cmd = new OleDbCommand(sql, cn))
        {
            Bind(cmd, args);
            DataTable table = new DataTable();
            using (OleDbDataAdapter da = new OleDbDataAdapter(cmd))
                da.Fill(table);
            return table;
        }
    }

    /// <summary>
    /// True once the database file is reachable and carries all three tables.
    ///
    /// Every request checks this before doing anything else, so the answer is
    /// cached after the first success - reading the schema means opening the
    /// Access file, which is not something to do three times per page view.
    /// </summary>
    public static bool IsInstalled()
    {
        if (_installed) return true;

        try
        {
            bool users = false, movies = false, ratings = false;

            using (OleDbConnection cn = Open())
            {
                DataTable schema = cn.GetSchema("Tables");
                foreach (DataRow row in schema.Rows)
                {
                    string name = Convert.ToString(row["TABLE_NAME"]);
                    if (String.Equals(name, "Users", StringComparison.OrdinalIgnoreCase)) users = true;
                    else if (String.Equals(name, "Movies", StringComparison.OrdinalIgnoreCase)) movies = true;
                    else if (String.Equals(name, "Ratings", StringComparison.OrdinalIgnoreCase)) ratings = true;
                }
            }

            _installed = users && movies && ratings;
        }
        catch
        {
            // No file yet, wrong provider, no permissions - Setup.aspx explains.
            _installed = false;
        }

        return _installed;
    }

    private static volatile bool _installed;

    // ----- DataRow readers, tolerant of NULLs -------------------------------

    public static string Str(DataRow row, string column)
    {
        if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) return "";
        return Convert.ToString(row[column]);
    }

    public static int Int(DataRow row, string column)
    {
        if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) return 0;
        return Convert.ToInt32(row[column]);
    }

    public static double Dbl(DataRow row, string column)
    {
        if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) return 0d;
        return Convert.ToDouble(row[column]);
    }

    public static bool Bool(DataRow row, string column)
    {
        if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) return false;
        return Convert.ToBoolean(row[column]);
    }

    public static DateTime? Date(DataRow row, string column)
    {
        if (!row.Table.Columns.Contains(column) || row[column] == DBNull.Value) return null;
        return Convert.ToDateTime(row[column]);
    }

    /// <summary>Access TEXT columns are capped at 255 characters.</summary>
    public static string Trim(string value, int max)
    {
        if (String.IsNullOrEmpty(value)) return value;
        return value.Length <= max ? value : value.Substring(0, max);
    }

    /// <summary>
    /// Value for an optional TEXT/MEMO column: trimmed to length, and NULL
    /// rather than "" when there is nothing to store.
    ///
    /// Access refuses a zero-length string in a column whose AllowZeroLength
    /// is off, and that default differs between Access versions, so optional
    /// text is always written as NULL. <see cref="Str"/> turns it back into ""
    /// on the way out, so nothing else has to care.
    /// </summary>
    public static object Text(string value, int max)
    {
        if (value == null) return DBNull.Value;
        value = value.Trim();
        if (value.Length == 0) return DBNull.Value;
        return Trim(value, max);
    }
}
