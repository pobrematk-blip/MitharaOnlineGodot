using Godot;
using System.Collections.Generic;
using System.Text;

public partial class PetColecaoComponent : Node
{
    public class PetColecaoEntry
    {
        public int PetID;
        public string Nome;
        public PetResource Recurso;
        public string CaminhoRecurso;
        public int Level = 1;
        public long Experience;
        public long ExperienceForNextLevel;
        public bool IsBossPet;
    }

    private readonly List<PetColecaoEntry> _pets = new();
    private static readonly Dictionary<int, (string path, string nome)> _cacheRecursos = new();

    [Signal]
    public delegate void ColecaoAtualizadaEventHandler();

    public void RegistrarCaptura(int petId, string petNome, int level = 1, long experience = 0L, long experienceForNextLevel = 0L, bool isBossPet = false)
    {
        for (int i = 0; i < _pets.Count; i++)
        {
            if (_pets[i].PetID == petId)
            {
                _pets[i].Nome = petNome;
                _pets[i].Level = Mathf.Max(1, level);
                _pets[i].Experience = System.Math.Max(0L, experience);
                _pets[i].ExperienceForNextLevel = System.Math.Max(0L, experienceForNextLevel);
                _pets[i].IsBossPet = isBossPet;
                EmitSignal(SignalName.ColecaoAtualizada);
                return;
            }
        }

        var entry = new PetColecaoEntry
        {
            PetID = petId,
            Nome = petNome,
            Level = Mathf.Max(1, level),
            Experience = System.Math.Max(0L, experience),
            ExperienceForNextLevel = System.Math.Max(0L, experienceForNextLevel),
            IsBossPet = isBossPet,
        };

        if (_cacheRecursos.TryGetValue(petId, out var cached))
        {
            entry.Recurso = ResourceLoader.Load<PetResource>(cached.path);
            entry.CaminhoRecurso = cached.path;
            if (entry.Recurso != null)
                entry.Nome = entry.Recurso.Nome;
        }

        _pets.Add(entry);

        if (entry.Recurso == null)
            ProcurarERegistrarRecurso(petId);

        EmitSignal(SignalName.ColecaoAtualizada);
        GD.Print($"[COLECAO] Pet '{petNome}' (ID:{petId}) registrado na colecao!");
    }

    public void Limpar()
    {
        _pets.Clear();
        EmitSignal(SignalName.ColecaoAtualizada);
    }

    public void ProcurarERegistrarRecurso(int petId)
    {
        for (int i = 0; i < _pets.Count; i++)
        {
            if (_pets[i].PetID != petId) continue;
            if (_pets[i].Recurso != null) return;

            string dir = "res://Pets/";
            var dirAccess = DirAccess.Open(dir);
            if (dirAccess == null)
            {
                GD.PrintErr($"[COLECAO] Diretorio '{dir}' nao existe!");
                return;
            }

            string nomeDesejado = NormalizarNome(_pets[i].Nome);
            dirAccess.ListDirBegin();
            string fileName = dirAccess.GetNext();
            while (!string.IsNullOrEmpty(fileName))
            {
                if (fileName.EndsWith(".tres") || fileName.EndsWith(".res"))
                {
                    string path = dir + fileName;
                    if (!ResourceLoader.Exists(path)) { fileName = dirAccess.GetNext(); continue; }
                    var res = ResourceLoader.Load<PetResource>(path);
                    bool mesmoId = res != null && res.PetID == petId;
                    bool mesmoNome = res != null
                        && !string.IsNullOrWhiteSpace(nomeDesejado)
                        && (NormalizarNome(res.Nome) == nomeDesejado
                            || NormalizarNome(System.IO.Path.GetFileNameWithoutExtension(fileName)) == nomeDesejado);
                    if (mesmoId || mesmoNome)
                    {
                        _pets[i].Recurso = res;
                        _pets[i].CaminhoRecurso = path;
                        _pets[i].Nome = res.Nome;
                        _cacheRecursos[petId] = (path, res.Nome);
                        GD.Print($"[COLECAO] Recurso encontrado: '{path}' para PetID {petId}");
                        dirAccess.ListDirEnd();
                        return;
                    }
                }
                fileName = dirAccess.GetNext();
            }
            dirAccess.ListDirEnd();

            GD.Print($"[COLECAO] Nenhum recurso encontrado para PetID {petId}");
            return;
        }
    }

    private static string NormalizarNome(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        string normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (char ch in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != System.Globalization.UnicodeCategory.NonSpacingMark && !char.IsWhiteSpace(ch))
                sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public bool TemPet(int petId)
    {
        for (int i = 0; i < _pets.Count; i++)
        {
            if (_pets[i].PetID == petId)
                return true;
        }
        return false;
    }

    public IReadOnlyList<PetColecaoEntry> GetPets()
    {
        return _pets;
    }
}
