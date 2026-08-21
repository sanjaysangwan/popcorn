using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.OleDb;
using System.Text.RegularExpressions;

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

    /// <summary>
    /// Builds the parameters for a command.
    ///
    /// Every parameter gets an explicit OleDbType rather than letting
    /// AddWithValue infer one. Inference is the source of two Access errors
    /// that say nothing about their cause: a DateTime is inferred as
    /// DBTimeStamp, which Jet and ACE reject against a DATETIME column with
    /// "Data type mismatch in criteria expression", and a long string is
    /// inferred as a sized VarWChar, which a MEMO column answers with "the
    /// field is too small to accept the amount of data".
    ///
    /// OleDb parameters are positional, so the order here has to match the
    /// order the "?" placeholders appear in the SQL.
    /// </summary>
    private static void Bind(OleDbCommand cmd, object[] args)
    {
        if (args == null) return;

        for (int i = 0; i < args.Length; i++)
            cmd.Parameters.Add(MakeParameter("p" + i, args[i]));
    }

    public static OleDbParameter MakeParameter(string name, object value)
    {
        OleDbParameter parameter = new OleDbParameter(name, TypeFor(value));
        parameter.Value = (value == null) ? DBNull.Value : value;
        return parameter;
    }

    /// <summary>
    /// The Access column type a .NET value belongs in. Kept separate from
    /// building the parameter so the mapping can be tested on its own.
    /// </summary>
    public static OleDbType TypeFor(object value)
    {
        // An untyped NULL: Access takes it in any column.
        if (value == null || value == DBNull.Value) return OleDbType.Variant;

        if (value is bool) return OleDbType.Boolean;            // YESNO
        if (value is DateTime) return OleDbType.Date;           // DATETIME

        if (value is int || value is short || value is long || value is byte)
            return OleDbType.Integer;                           // LONG / AUTOINCREMENT

        if (value is double || value is float || value is decimal)
            return OleDbType.Double;

        string text = value as string;
        if (text != null)
            return text.Length > TextColumnLimit
                       ? OleDbType.LongVarWChar                 // MEMO
                       : OleDbType.VarWChar;                    // TEXT(n)

        return OleDbType.Variant;
    }

    /// <summary>
    /// Access error messages never mention the statement that produced them,
    /// which is not much help on a host with no logs to read. This puts it
    /// back so the page showing the error also shows the cause.
    /// </summary>
    private static Exception Explain(Exception ex, string sql)
    {
        string statement = Regex.Replace(sql ?? "", @"\s+", " ").Trim();
        if (statement.Length > 120) statement = statement.Substring(0, 120) + "...";

        return new InvalidOperationException(
            ex.Message + "  [while running: " + statement + "]", ex);
    }

    public static int Execute(string sql, params object[] args)
    {
        try
        {
            using (OleDbConnection cn = Open())
            using (OleDbCommand cmd = new OleDbCommand(sql, cn))
            {
                Bind(cmd, args);
                return cmd.ExecuteNonQuery();
            }
        }
        catch (OleDbException ex) { throw Explain(ex, sql); }
    }

    public static object Scalar(string sql, params object[] args)
    {
        try
        {
            using (OleDbConnection cn = Open())
            using (OleDbCommand cmd = new OleDbCommand(sql, cn))
            {
                Bind(cmd, args);
                object o = cmd.ExecuteScalar();
                return (o == DBNull.Value) ? null : o;
            }
        }
        catch (OleDbException ex) { throw Explain(ex, sql); }
    }

    /// <summary>Runs an INSERT and returns the new AutoNumber key.</summary>
    public static int Insert(string sql, params object[] args)
    {
        try
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
        catch (OleDbException ex) { throw Explain(ex, sql); }
    }

    public static DataTable Query(string sql, params object[] args)
    {
        try
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
        catch (OleDbException ex) { throw Explain(ex, sql); }
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

    /// <summary>
    /// The columns a table actually ended up with, as "Name  Type(size)".
    ///
    /// Worth being able to see: the DDL asks for TEXT(150) and YESNO, but what
    /// Jet and ACE make of that differs, and a column that came out as the
    /// wrong type shows up later as a type-mismatch error a long way from here.
    /// </summary>
    public static List<string> DescribeColumns(string tableName)
    {
        List<string> columns = new List<string>();
        try
        {
            using (OleDbConnection cn = Open())
            {
                DataTable schema = cn.GetSchema("Columns",
                    new string[] { null, null, tableName, null });

                List<DataRow> rows = new List<DataRow>();
                foreach (DataRow row in schema.Rows) rows.Add(row);
                rows.Sort(delegate(DataRow a, DataRow b)
                {
                    return Int(a, "ORDINAL_POSITION").CompareTo(Int(b, "ORDINAL_POSITION"));
                });

                foreach (DataRow row in rows)
                {
                    string type = Enum.GetName(typeof(OleDbType), Int(row, "DATA_TYPE"))
                                  ?? ("type " + Int(row, "DATA_TYPE"));
                    int size = Int(row, "CHARACTER_MAXIMUM_LENGTH");

                    columns.Add(Str(row, "COLUMN_NAME") + "  -  " + type +
                                (size > 0 ? " (" + size + ")" : ""));
                }
            }
        }
        catch (Exception ex)
        {
            columns.Add("(could not be read: " + ex.Message + ")");
        }
        return columns;
    }

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
