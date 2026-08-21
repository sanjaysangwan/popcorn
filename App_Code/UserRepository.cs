using System;
using System.Collections.Generic;
using System.Data;

public static class UserRepository
{
    private const string SelectList =
        "SELECT UserId, Email, DisplayName, IsApproved, IsAdmin, IsDisabled, " +
        "CreatedUtc, ApprovedUtc, LastLoginUtc FROM Users ";

    public static FamilyUser GetById(int userId)
    {
        DataTable t = Db.Query(SelectList + "WHERE UserId = ?", userId);
        return t.Rows.Count == 0 ? null : FamilyUser.FromRow(t.Rows[0]);
    }

    public static FamilyUser GetByEmail(string email)
    {
        DataTable t = Db.Query(SelectList + "WHERE Email = ?", Normalise(email));
        return t.Rows.Count == 0 ? null : FamilyUser.FromRow(t.Rows[0]);
    }

    public static bool EmailInUse(string email)
    {
        object count = Db.Scalar("SELECT COUNT(*) FROM Users WHERE Email = ?", Normalise(email));
        return Convert.ToInt32(count ?? 0) > 0;
    }

    public static int Count()
    {
        return Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM Users") ?? 0);
    }

    public static int PendingCount()
    {
        return Convert.ToInt32(
            Db.Scalar("SELECT COUNT(*) FROM Users WHERE IsApproved = False AND IsDisabled = False") ?? 0);
    }

    /// <summary>
    /// Creates an account. New members always start unapproved - an administrator
    /// has to let them in - except for the very first account, which becomes the
    /// administrator so the site is usable at all.
    /// </summary>
    public static int Create(string email, string displayName, string password,
                             bool approved, bool isAdmin)
    {
        string hash, salt;
        PasswordHasher.CreateHash(password, out hash, out salt);

        // ApprovedUtc is left out of the statement entirely for a pending
        // account, rather than passed as a null date parameter - Access is
        // fussy about untyped NULLs in DATETIME columns.
        if (!approved)
        {
            return Db.Insert(
                "INSERT INTO Users (Email, DisplayName, PasswordHash, PasswordSalt, IsApproved, " +
                "IsAdmin, IsDisabled, CreatedUtc) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                Normalise(email), Db.Trim(displayName, 80), hash, salt,
                false, isAdmin, false, DateTime.UtcNow);
        }

        return Db.Insert(
            "INSERT INTO Users (Email, DisplayName, PasswordHash, PasswordSalt, IsApproved, " +
            "IsAdmin, IsDisabled, CreatedUtc, ApprovedUtc) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)",
            Normalise(email), Db.Trim(displayName, 80), hash, salt,
            true, isAdmin, false, DateTime.UtcNow, DateTime.UtcNow);
    }

    /// <summary>Returns the user when the password is right, otherwise null.</summary>
    public static FamilyUser Authenticate(string email, string password)
    {
        DataTable t = Db.Query(
            "SELECT UserId, PasswordHash, PasswordSalt FROM Users WHERE Email = ?",
            Normalise(email));
        if (t.Rows.Count == 0) return null;

        DataRow row = t.Rows[0];
        if (!PasswordHasher.Verify(password, Db.Str(row, "PasswordHash"), Db.Str(row, "PasswordSalt")))
            return null;

        return GetById(Db.Int(row, "UserId"));
    }

    public static void RecordLogin(int userId)
    {
        Db.Execute("UPDATE Users SET LastLoginUtc = ? WHERE UserId = ?", DateTime.UtcNow, userId);
    }

    public static void SetPassword(int userId, string password)
    {
        string hash, salt;
        PasswordHasher.CreateHash(password, out hash, out salt);
        Db.Execute("UPDATE Users SET PasswordHash = ?, PasswordSalt = ? WHERE UserId = ?",
                   hash, salt, userId);
    }

    public static void Approve(int userId)
    {
        Db.Execute("UPDATE Users SET IsApproved = ?, IsDisabled = ?, ApprovedUtc = ? WHERE UserId = ?",
                   true, false, DateTime.UtcNow, userId);
    }

    public static void SetDisabled(int userId, bool disabled)
    {
        Db.Execute("UPDATE Users SET IsDisabled = ? WHERE UserId = ?", disabled, userId);
    }

    public static void SetAdmin(int userId, bool isAdmin)
    {
        Db.Execute("UPDATE Users SET IsAdmin = ? WHERE UserId = ?", isAdmin, userId);
    }

    /// <summary>Removes a member together with every rating they left.</summary>
    public static void Delete(int userId)
    {
        Db.Execute("DELETE FROM Ratings WHERE UserId = ?", userId);
        Db.Execute("DELETE FROM Users WHERE UserId = ?", userId);
    }

    public static int AdminCount()
    {
        return Convert.ToInt32(
            Db.Scalar("SELECT COUNT(*) FROM Users WHERE IsAdmin = True AND IsDisabled = False") ?? 0);
    }

    public static List<FamilyUser> All()
    {
        DataTable t = Db.Query(SelectList + "ORDER BY IsApproved, CreatedUtc DESC");
        List<FamilyUser> list = new List<FamilyUser>();
        foreach (DataRow row in t.Rows) list.Add(FamilyUser.FromRow(row));
        return list;
    }

    /// <summary>Approved, active members - the "family" for rating purposes.</summary>
    public static List<FamilyUser> ActiveMembers()
    {
        DataTable t = Db.Query(SelectList +
            "WHERE IsApproved = True AND IsDisabled = False ORDER BY DisplayName");
        List<FamilyUser> list = new List<FamilyUser>();
        foreach (DataRow row in t.Rows) list.Add(FamilyUser.FromRow(row));
        return list;
    }

    public static string Normalise(string email)
    {
        return (email ?? "").Trim().ToLowerInvariant();
    }
}
