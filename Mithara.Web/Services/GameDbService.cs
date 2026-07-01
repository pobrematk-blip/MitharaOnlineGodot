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
        return Convert.ToHexString(hash);
    }

    public (int? id, string? error) CreateAccount(string username, string email, string password)
    {
        using var conn = CreateConnection();

        using var check = conn.CreateCommand();
        check.CommandText = "SELECT id FROM accounts WHERE username = @u OR LOWER(email) = LOWER(@e)";
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
        cmd.CommandText = "SELECT id, password_hash, salt FROM accounts WHERE username = @u";
        cmd.Parameters.AddWithValue("@u", username);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return (null, "Usu\u00E1rio ou senha inv\u00E1lidos.");

        var hash = reader.GetString(1);
        var salt = reader.IsDBNull(2) ? "" : reader.GetString(2);
        if (hash != HashPassword(password, salt))
            return (null, "Usu\u00E1rio ou senha inv\u00E1lidos.");

        return (reader.GetInt32(0), null);
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
        cmd.CommandText = "SELECT COUNT(*) FROM accounts WHERE id = @i AND (username LIKE 'admin%' OR email = 'admin@mithara.local')";
        cmd.Parameters.AddWithValue("@i", accountId);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }
}
