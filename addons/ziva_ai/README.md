# Ziva AI - Godot Plugin

## 🚀 Instalação

O plugin já está instalado em `addons/ziva_ai/`

### Ativar o Plugin

1. Abra seu projeto Godot
2. Vá para: **Project > Project Settings > Plugins**
3. Procure por **"Ziva AI"**
4. Clique no checkbox para **Enable**
5. Reinicie a Godot se necessário

## 🔑 Obter API Key

1. Entre em [ziva.sh](https://ziva.sh)
2. Faça login ou crie uma conta
3. Vá para **API Settings** ou **Account**
4. Copie sua **API Key**

## 📝 Como Usar

1. Após ativar o plugin, uma aba aparecerá no lado direito (DOCK)
2. Cole sua API Key no campo "API Key:"
3. Clique em "🔗 Conectar"
4. Digite seu prompt (ex: "Crie um sistema de inimigos")
5. Clique em "✉️ Enviar"

## 📚 Exemplos de Prompts

- `Crie um script de movimento 2D para o Player`
- `Adicione um sistema de inventário`
- `Corrija os erros no arquivo Player.cs`
- `Crie inimigos com pathfinding`
- `Implemente um sistema de XP e níveis`

## ⚙️ Configuração Avançada

Se precisar editar as configurações do plugin, edite:
- `addons/ziva_ai/plugin.cfg` - Configurações gerais
- `addons/ziva_ai/plugin.gd` - Lógica do plugin
- `addons/ziva_ai/ziva_dock.gd` - Interface da aba

## 🐛 Troubleshooting

### Plugin não aparece
- Verifique se o arquivo `plugin.cfg` está na pasta `addons/ziva_ai/`
- Reinicie a Godot
- Verifique em Project > Project Settings > Plugins

### API Key não funciona
- Verifique se a chave está correta
- Regenere a chave em [ziva.sh](https://ziva.sh)
- Certifique-se de estar conectado à internet

### Resposta vazia
- Verifique sua conexão de internet
- Tente um prompt mais simples
- Verifique os logs em View > Output

## 📞 Suporte

- Site: https://ziva.sh
- Documentação: https://docs.ziva.sh
- Email: support@ziva.sh

---

**Plugin instalado e pronto para usar!** 🎮
