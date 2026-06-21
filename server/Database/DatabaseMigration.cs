using Microsoft.Data.Sqlite;

namespace Mithara.Server.Database;

public static class DatabaseMigration
{
    public static void MigrateFromSqlite(string sqlitePath, DatabaseManager destDb)
    {
        if (!File.Exists(sqlitePath))
        {
            Logger.Info($"[Migra??o] Arquivo SQLite n?o encontrado: {sqlitePath}");
            return;
        }

        Logger.Info("[Migracao] Verificando se PostgreSQL ja possui dados...");
        if (destDb.GetCharacterCount() > 0)
        {
            Logger.Info("[Migracao] PostgreSQL ja possui dados. Pulando migracao.");
            return;
        }

        Logger.Info("[Migracao] Iniciando migracao de SQLite para PostgreSQL...");

        string sqliteConn = new SqliteConnectionStringBuilder
        {
            DataSource = sqlitePath,
            Mode = SqliteOpenMode.ReadOnly,
        }.ToString();

        using var src = new SqliteConnection(sqliteConn);
        src.Open();

        MigrateAccounts(src, destDb);
        MigrateCharacters(src, destDb);
        MigrateItems(src, destDb);
        MigrateGuilds(src, destDb);
        MigrateGuildMembers(src, destDb);
        MigrateGuildSkills(src, destDb);
        MigratePlayerQuests(src, destDb);
        MigrateCharacterPets(src, destDb);

        Logger.Info("[Migracao] Migracao concluida com sucesso!");
    }

    private static int MigrateAccounts(SqliteConnection src, DatabaseManager dest)
    {
        using var cmd = src.CreateCommand();
        cmd.CommandText = "SELECT id, username, password_hash, security_question, security_answer, salt FROM accounts";
        int count = 0;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            int id = reader.GetInt32(0);
            string username = reader.GetString(1);
            string hash = reader.GetString(2);
            string secQ = reader.IsDBNull(3) ? "" : reader.GetString(3);
            string secA = reader.IsDBNull(4) ? "" : reader.GetString(4);
            string salt = reader.IsDBNull(5) ? "" : reader.GetString(5);
            dest.InsertAccount(id, username, hash, secQ, secA, salt);
            count++;
        }
        Logger.Info($"[Migracao] {count} contas migradas.");
        return count;
    }

    private static int MigrateCharacters(SqliteConnection src, DatabaseManager dest)
    {
        using var cmd = src.CreateCommand();
        cmd.CommandText = "SELECT id, account_id, slot_index, name, class, race, level, xp, forca, agilidade, destreza, inteligencia, pos_x, pos_y, bank_gold, gold, stat_points FROM characters";
        int count = 0;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            dest.InsertCharacter(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetInt32(6),
                reader.GetInt64(7),
                reader.GetInt32(8),
                reader.GetInt32(9),
                reader.GetInt32(10),
                reader.GetInt32(11),
                (float)reader.GetDouble(12),
                (float)reader.GetDouble(13),
                reader.GetInt32(14),
                reader.GetInt32(15),
                reader.GetInt32(16)
            );
            count++;
        }
        Logger.Info($"[Migracao] {count} personagens migrados.");
        return count;
    }

    private static int MigrateItems(SqliteConnection src, DatabaseManager dest)
    {
        using var cmd = src.CreateCommand();
        cmd.CommandText = "SELECT id, character_id, slot, item_id, quantity FROM items";
        int count = 0;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            dest.InsertItem(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4));
            count++;
        }
        Logger.Info($"[Migracao] {count} itens migrados.");
        return count;
    }

    private static int MigrateGuilds(SqliteConnection src, DatabaseManager dest)
    {
        using var cmd = src.CreateCommand();
        cmd.CommandText = "SELECT id, name, level, xp, skill_points FROM guilds";
        int count = 0;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            dest.InsertGuild(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4));
            count++;
        }
        Logger.Info($"[Migracao] {count} guildas migradas.");
        return count;
    }

    private static int MigrateGuildMembers(SqliteConnection src, DatabaseManager dest)
    {
        using var cmd = src.CreateCommand();
        cmd.CommandText = "SELECT guild_id, entity_id, name, rank FROM guild_members";
        int count = 0;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            dest.InsertGuildMember(reader.GetInt32(0), (ulong)reader.GetInt64(1), reader.GetString(2), reader.GetInt32(3));
            count++;
        }
        Logger.Info($"[Migracao] {count} membros de guildas migrados.");
        return count;
    }

    private static int MigrateGuildSkills(SqliteConnection src, DatabaseManager dest)
    {
        using var cmd = src.CreateCommand();
        cmd.CommandText = "SELECT guild_id, skill_id, level FROM guild_skills";
        int count = 0;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            dest.InsertGuildSkill(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2));
            count++;
        }
        Logger.Info($"[Migracao] {count} habilidades de guildas migradas.");
        return count;
    }

    private static int MigratePlayerQuests(SqliteConnection src, DatabaseManager dest)
    {
        using var cmd = src.CreateCommand();
        cmd.CommandText = "SELECT id, character_id, quest_id, progress, completed, claimed FROM player_quests";
        int count = 0;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            dest.InsertPlayerQuest(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2),
                reader.IsDBNull(3) ? "[]" : reader.GetString(3),
                reader.GetInt32(4) != 0, reader.GetInt32(5) != 0);
            count++;
        }
        Logger.Info($"[Migracao] {count} missoes migradas.");
        return count;
    }

    private static int MigrateCharacterPets(SqliteConnection src, DatabaseManager dest)
    {
        using var cmd = src.CreateCommand();
        cmd.CommandText = "SELECT id, character_id, pet_id, pet_name FROM character_pets";
        int count = 0;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            dest.InsertCharacterPet(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2),
                reader.IsDBNull(3) ? "" : reader.GetString(3));
            count++;
        }
        Logger.Info($"[Migracao] {count} pets migrados.");
        return count;
    }
}
