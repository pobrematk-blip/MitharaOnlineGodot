using System.Security.Cryptography;
using System.Text;
using Mithara.Web.Models;
using Npgsql;

namespace Mithara.Web.Services;

public class GameDbService
{
    private readonly string _connectionString;

    public GameDbService(string connectionString)
    {
        _connectionString = connectionString;
    }

    private NpgsqlConnection CreateConnection()
    {
        var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private static string GenerateSalt()
    {
        var saltBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(saltBytes);
        return Convert.ToHexString(saltBytes);
    }

    private static string HashPassword(string password, string salt)
    {
        var combined = Encoding.UTF8.GetBytes(password + salt);
        var hash = SHA256.HashData(combined);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GenerateResetToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public (int? id, string? error) CreateAccount(string username, string email, string password)
    {
        using var conn = CreateConnection();

        using var check = conn.CreateCommand();
        check.CommandText = "SELECT id FROM accounts WHERE LOWER(username) = LOWER(@u) OR LOWER(email) = LOWER(@e)";
        check.Parameters.AddWithValue("@u", username);
        check.Parameters.AddWithValue("@e", email);
        var exists = check.ExecuteScalar();
        if (exists != null)
            return (null, "Usu\u00E1rio ou email j\u00E1 cadastrado.");

        using var cmd = conn.CreateCommand();
        string salt = GenerateSalt();
        cmd.CommandText = """
            INSERT INTO accounts (username, email, password_hash, salt)
            VALUES (@u, @e, @p, @s) RETURNING id
            """;
        cmd.Parameters.AddWithValue("@u", username);
        cmd.Parameters.AddWithValue("@e", email);
        cmd.Parameters.AddWithValue("@p", HashPassword(password, salt));
        cmd.Parameters.AddWithValue("@s", salt);
        return (Convert.ToInt32(cmd.ExecuteScalar()), null);
    }

    public (int? id, string? error) LoginAccount(string username, string password)
    {
        using var conn = CreateConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, username, password_hash, salt FROM accounts WHERE LOWER(username) = LOWER(@u) OR LOWER(email) = LOWER(@u)";
        cmd.Parameters.AddWithValue("@u", username);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return (null, "Usu\u00E1rio ou senha inv\u00E1lidos.");

        var hash = reader.GetString(2);
        var salt = reader.IsDBNull(3) ? "" : reader.GetString(3);
        if (!string.Equals(hash, HashPassword(password, salt), StringComparison.OrdinalIgnoreCase))
            return (null, "Usu\u00E1rio ou senha inv\u00E1lidos.");

        return (reader.GetInt32(0), null);
    }

    public (string token, string email, string username)? CreatePasswordResetRequest(string usernameOrEmail)
    {
        using var conn = CreateConnection();

        using var find = conn.CreateCommand();
        find.CommandText = """
            SELECT id, username, email
            FROM accounts
            WHERE LOWER(username) = LOWER(@v) OR LOWER(email) = LOWER(@v)
            LIMIT 1
            """;
        find.Parameters.AddWithValue("@v", usernameOrEmail.Trim());

        using var reader = find.ExecuteReader();
        if (!reader.Read())
            return null;

        int accountId = reader.GetInt32(0);
        string username = reader.GetString(1);
        string email = reader.GetString(2);
        reader.Close();

        string token = GenerateResetToken();
        string tokenHash = HashToken(token);

        using var revoke = conn.CreateCommand();
        revoke.CommandText = """
            UPDATE account_password_resets
            SET used_at = NOW()
            WHERE account_id = @a AND used_at IS NULL
            """;
        revoke.Parameters.AddWithValue("@a", accountId);
        revoke.ExecuteNonQuery();

        using var insert = conn.CreateCommand();
        insert.CommandText = """
            INSERT INTO account_password_resets (account_id, token_hash, expires_at)
            VALUES (@a, @t, NOW() + INTERVAL '30 minutes')
            """;
        insert.Parameters.AddWithValue("@a", accountId);
        insert.Parameters.AddWithValue("@t", tokenHash);
        insert.ExecuteNonQuery();

        return (token, email, username);
    }

    public bool ResetPasswordWithToken(string token, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(token) || newPassword.Length < 6)
            return false;

        using var conn = CreateConnection();
        using var tx = conn.BeginTransaction();

        using var find = conn.CreateCommand();
        find.Transaction = tx;
        find.CommandText = """
            SELECT id, account_id
            FROM account_password_resets
            WHERE token_hash = @t
              AND used_at IS NULL
              AND expires_at > NOW()
            FOR UPDATE
            """;
        find.Parameters.AddWithValue("@t", HashToken(token.Trim()));

        using var reader = find.ExecuteReader();
        if (!reader.Read())
            return false;

        int resetId = reader.GetInt32(0);
        int accountId = reader.GetInt32(1);
        reader.Close();

        string salt = GenerateSalt();
        using var updateAccount = conn.CreateCommand();
        updateAccount.Transaction = tx;
        updateAccount.CommandText = "UPDATE accounts SET password_hash = @p, salt = @s WHERE id = @a";
        updateAccount.Parameters.AddWithValue("@p", HashPassword(newPassword, salt));
        updateAccount.Parameters.AddWithValue("@s", salt);
        updateAccount.Parameters.AddWithValue("@a", accountId);
        updateAccount.ExecuteNonQuery();

        using var markUsed = conn.CreateCommand();
        markUsed.Transaction = tx;
        markUsed.CommandText = "UPDATE account_password_resets SET used_at = NOW() WHERE id = @i";
        markUsed.Parameters.AddWithValue("@i", resetId);
        markUsed.ExecuteNonQuery();

        tx.Commit();
        return true;
    }

    public AccountInfo? GetAccountInfo(int accountId)
    {
        using var conn = CreateConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, username, email, created_at, vip_expiry FROM accounts WHERE id = @i";
        cmd.Parameters.AddWithValue("@i", accountId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        var account = new AccountInfo
        {
            Id = reader.GetInt32(0),
            Username = reader.GetString(1),
            Email = reader.GetString(2),
            CreatedAt = reader.GetDateTime(3),
            VipExpiry = reader.GetDateTime(4),
        };
        reader.Close();

        using var charCmd = conn.CreateCommand();
        charCmd.CommandText = """
            SELECT id, name, class, race, level, xp, gold, bank_gold, current_map
            FROM characters WHERE account_id = @a ORDER BY slot_index
            """;
        charCmd.Parameters.AddWithValue("@a", accountId);
        using var charReader = charCmd.ExecuteReader();
        while (charReader.Read())
        {
            account.Characters.Add(new CharacterInfo
            {
                Id = charReader.GetInt32(0),
                Name = charReader.GetString(1),
                Class = charReader.GetString(2),
                Race = charReader.GetString(3),
                Level = charReader.GetInt32(4),
                Xp = charReader.GetInt64(5),
                Gold = charReader.GetInt32(6),
                BankGold = charReader.GetInt32(7),
                CurrentMap = charReader.GetString(8),
            });
        }

        return account;
    }

    public List<GuildInfo> GetGuildRanking(int top = 50)
    {
        var result = new List<GuildInfo>();
        using var conn = CreateConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT g.id, g.name, g.level, g.xp, g.created_at,
                   COALESCE(m.member_count, 0) AS member_count,
                   COALESCE(l.name, '') AS leader_name
            FROM guilds g
            LEFT JOIN (SELECT guild_id, COUNT(*) AS member_count FROM guild_members GROUP BY guild_id) m ON m.guild_id = g.id
            LEFT JOIN guild_members l ON l.guild_id = g.id AND l.rank = 1
            ORDER BY g.level DESC, g.xp DESC
            LIMIT @t
            """;
        cmd.Parameters.AddWithValue("@t", top);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new GuildInfo
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Level = reader.GetInt32(2),
                Xp = reader.GetInt32(3),
                CreatedAt = reader.GetDateTime(4),
                MemberCount = reader.GetInt32(5),
                LeaderName = reader.GetString(6),
            });
        }
        return result;
    }

    public List<CharacterInfo> GetCharacterRanking(int top = 50)
    {
        var result = new List<CharacterInfo>();
        using var conn = CreateConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, name, class, race, level, xp, gold
            FROM characters ORDER BY level DESC, xp DESC LIMIT @t
            """;
        cmd.Parameters.AddWithValue("@t", top);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new CharacterInfo
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Class = reader.GetString(2),
                Race = reader.GetString(3),
                Level = reader.GetInt32(4),
                Xp = reader.GetInt64(5),
                Gold = reader.GetInt32(6),
            });
        }
        return result;
    }

    public int GetTotalAccounts()
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM accounts";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public int GetTotalCharacters()
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM characters";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public int GetTotalGuilds()
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM guilds";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public List<AccountInfo> GetAllAccounts(int page, int pageSize)
    {
        var result = new List<AccountInfo>();
        using var conn = CreateConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, username, email, created_at FROM accounts ORDER BY id LIMIT @limit OFFSET @offset";
        cmd.Parameters.AddWithValue("@limit", pageSize);
        cmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new AccountInfo
            {
                Id = reader.GetInt32(0),
                Username = reader.GetString(1),
                Email = reader.GetString(2),
                CreatedAt = reader.GetDateTime(3),
            });
        }
        return result;
    }

    public bool IsAdmin(int accountId)
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT is_admin FROM accounts WHERE id = @i";
        cmd.Parameters.AddWithValue("@i", accountId);
        var result = cmd.ExecuteScalar();
        return result != null && (bool)result;
    }

    public void EnsureAdminColumn()
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'accounts' AND column_name = 'is_admin'
                ) THEN
                    ALTER TABLE accounts ADD COLUMN is_admin BOOLEAN NOT NULL DEFAULT FALSE;
                    UPDATE accounts SET is_admin = TRUE WHERE username LIKE 'admin%' OR email = 'admin@mithara.local';
                END IF;
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'accounts' AND column_name = 'last_seen'
                ) THEN
                    ALTER TABLE accounts ADD COLUMN last_seen TIMESTAMP NOT NULL DEFAULT '2000-01-01 00:00:00';
                END IF;
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'accounts' AND column_name = 'online_character_name'
                ) THEN
                    ALTER TABLE accounts ADD COLUMN online_character_name VARCHAR(255) NOT NULL DEFAULT '';
                END IF;
            END $$;
            """;
        cmd.ExecuteNonQuery();
    }

    public void EnsurePasswordResetTables()
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS account_password_resets (
                id SERIAL PRIMARY KEY,
                account_id INTEGER NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
                token_hash VARCHAR(128) NOT NULL UNIQUE,
                created_at TIMESTAMP NOT NULL DEFAULT NOW(),
                expires_at TIMESTAMP NOT NULL,
                used_at TIMESTAMP NULL
            );

            CREATE INDEX IF NOT EXISTS ix_account_password_resets_account
                ON account_password_resets(account_id);

            CREATE INDEX IF NOT EXISTS ix_account_password_resets_valid
                ON account_password_resets(token_hash, expires_at)
                WHERE used_at IS NULL;
            """;
        cmd.ExecuteNonQuery();
    }

    public void UpdateLastSeen(int accountId)
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE accounts SET last_seen = NOW() WHERE id = @i";
        cmd.Parameters.AddWithValue("@i", accountId);
        cmd.ExecuteNonQuery();
    }

    public int GetOnlinePlayerCount()
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM accounts WHERE last_seen > NOW() - INTERVAL '5 minutes'";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public List<string> GetOnlinePlayerNames()
    {
        var names = new List<string>();
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT DISTINCT online_character_name
            FROM accounts
            WHERE last_seen > NOW() - INTERVAL '5 minutes'
              AND online_character_name <> ''
            ORDER BY online_character_name
            LIMIT 20
            """;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            names.Add(reader.GetString(0));
        return names;
    }

    public SiteStatusSnapshot GetSiteStatusSnapshot()
    {
        return new SiteStatusSnapshot
        {
            OnlinePlayers = GetOnlinePlayerCount(),
            OnlinePlayerNames = GetOnlinePlayerNames(),
            TotalAccounts = GetTotalAccounts(),
            TotalCharacters = GetTotalCharacters(),
            TotalGuilds = GetTotalGuilds(),
            ServerTimeUtc = DateTime.UtcNow,
        };
    }
}
