#nullable enable
using Godot;

public partial class MenuInicial : Node
{
    public override void _Ready()
    {
        GameNetwork? net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net == null || !net.IsConnected)
        {
            CallDeferred(nameof(AguardarLogin));
        }
        else if (!net.LoggedIn)
        {
            CallDeferred(nameof(AguardarLogin));
        }
        else
        {
            CallDeferred(nameof(IrParaProximaCena));
        }
    }

    private void AguardarLogin()
    {
        GameNetwork? net = GetNodeOrNull<GameNetwork>("/root/GameNetwork");
        if (net != null && net.IsConnected && net.LoggedIn)
        {
            IrParaProximaCena();
            return;
        }

        var login = new TelaLogin();
        AddChild(login);
    }

    public void IrParaProximaCena()
    {
        var escolhido = GetNode<PersonagemEscolhido>("/root/PersonagemEscolhido");
        bool temPersonagem = escolhido.TotalSlotsOcupados() > 0;
        string proximaCena = temPersonagem
            ? SceneConstants.SELECAO_PERSONAGEM
            : SceneConstants.INTRO_HISTORIA;
        GetTree().ChangeSceneToFile(proximaCena);
    }
}
