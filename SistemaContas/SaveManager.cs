using Godot;
using System.Collections.Generic;

public partial class SaveManager : Node
{
    public const string ContaPath = "user://conta.cfg";

    private string SaveDir => AccountId == 0
        ? "user://characters/"
        : $"user://characters_{AccountId}/";

    private int AccountId
    {
        get
        {
            var net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
            return net?.AccountId ?? 0;
        }
    }
    private const string PrefixoSlot = "slot_";
    private const string Extensao = ".cfg";

    private ContaData _contaData;

    public ContaData Conta
    {
        get
        {
            if (_contaData == null) CarregarConta();
            return _contaData;
        }
    }

    public int SlotsDisponiveis => Conta.SlotsMaximos;

    public override void _Ready()
    {
        _contaData = new ContaData();
        GD.Print("[SAVE] SaveManager local desativado. O jogo e 100% online.");
    }

    private void GarantirPasta()
    {
        return;
    }

    public void CarregarConta()
    {
        _contaData = new ContaData();
    }

    public void SalvarConta()
    {
        GD.Print("[SAVE] SalvarConta local bloqueado.");
    }

    public string SlotPath(int slotIndex)
    {
        return SaveDir + PrefixoSlot + slotIndex + Extensao;
    }

    public bool SlotOcupado(int slotIndex)
    {
        return false;
    }

    public int EncontrarSlotVazio()
    {
        return -1;
    }

    public int TotalSlotsOcupados()
    {
        return 0;
    }

    public List<int> SlotsOcupados()
    {
        return new List<int>();
    }

    public bool SalvarSlot(int slotIndex, PersonagemEscolhido personagem)
    {
        GD.PrintErr("[SAVE] SalvarSlot bloqueado. Personagens devem ser salvos no servidor.");
        return false;
    }

    public bool CarregarSlot(int slotIndex, PersonagemEscolhido personagem)
    {
        GD.PrintErr("[SAVE] CarregarSlot bloqueado. Personagens devem vir do servidor.");
        return false;
    }

    public bool DeletarSlot(int slotIndex)
    {
        GD.PrintErr("[SAVE] DeletarSlot local bloqueado. Exclusao deve passar pelo servidor.");
        return false;
    }

    public void SalvarInventario(System.Collections.Generic.List<SlotInventario> slots, int? slotIndex = null)
    {
        GD.PrintErr("[SAVE] SalvarInventario local bloqueado. Inventario deve ser salvo no servidor.");
    }

    public void CarregarInventario(System.Collections.Generic.List<SlotInventario> slots, ItemDatabase itemDB, int? slotIndex = null)
    {
        GD.PrintErr("[SAVE] CarregarInventario local bloqueado. Inventario deve vir do servidor.");
    }

    public bool IsAdmin => false;

    public void SetAdmin(bool ativo)
    {
        GD.PrintErr("[SAVE] Admin local bloqueado. Permissoes devem vir do servidor.");
    }

    public bool ComprarSlot()
    {
        GD.PrintErr("[SAVE] ComprarSlot local bloqueado. Compra deve passar pelo servidor.");
        return false;
    }
}
