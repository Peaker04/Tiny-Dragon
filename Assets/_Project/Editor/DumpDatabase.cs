using UnityEditor;
using UnityEngine;
using System.IO;
using System.Data;
using TinyDragon.Data;

public static class DumpDatabase
{
    [MenuItem("Tiny-Dragon/Dump Skills Database")]
    public static void DumpSkills()
    {
        string dbPath = Path.Combine(Application.persistentDataPath, "tiny_dragon.db");
        Debug.Log($"Dumping database from: {dbPath}");
        if (!File.Exists(dbPath))
        {
            Debug.LogError("Database file not found!");
            return;
        }

        using (SqliteDatabase db = new SqliteDatabase(dbPath))
        {
            if (!db.Open())
            {
                Debug.LogError("Failed to open database!");
                return;
            }

            using (IDbCommand command = db.CreateCommand("SELECT id, name, description, skillType FROM Skill;"))
            {
                using (IDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string id = reader.GetString(0);
                        string name = reader.GetString(1);
                        string desc = reader.IsDBNull(2) ? "NULL" : reader.GetString(2);
                        string type = reader.GetString(3);
                        Debug.Log($"Skill ID: {id} | Name: {name} | Desc: {desc} | Type: {type}");
                    }
                }
            }
        }
    }
}
