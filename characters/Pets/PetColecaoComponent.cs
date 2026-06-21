using Godot;
using System.Collections.Generic;

public partial class PetColecaoComponent : Node
{
    public class PetColecaoEntry
    {
        public int PetID;
        public string Nome;
        public PetResource Recurso;
        public string CaminhoRecurso;
    }

    private readonly List<PetColecaoEntry> _pets = new();
    private static readonly Dictionary<int, (string path, string nome)> _cacheRecursos = new();

    [Signal]
    public delegate void ColecaoAtualizadaEventHandler();

    public void RegistrarCaptura(int petId, string petNome)
    {
        for (int i = 0; i < _pets.Count; i++)
        {
            if (_pets[i].PetID == petId)
                return;
        }

        var entry = new PetColecaoEntry
        {
            PetID = petId,
            Nome = petNome,
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
                GD.PrintErr($"[COLE??O] Diret?rio '{dir}' n?o existe!");
                return;
            }

            dirAccess.ListDirBegin();
            string fileName = dirAccess.GetNext();
            while (!string.IsNullOrEmpty(fileName))
            {
                if (fileName.EndsWith(".tres") || fileName.EndsWith(".res"))
                {
                    string path = dir + fileName;
                    if (!ResourceLoader.Exists(path)) { fileName = dirAccess.GetNext(); continue; }
                    var res = ResourceLoader.Load<PetResource>(path);
                    if (res != null && res.PetID == petId)
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
