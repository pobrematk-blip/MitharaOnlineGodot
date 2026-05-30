# 🎲 Sistema de Dano Variado e Crítico Melhorado

## ✅ Status: IMPLEMENTADO E COMPILADO

Implementação completa do sistema de dano com variação (min/max), Destreza focada em crítico, e bônus de dano crítico por items.

---

## 📋 Mudanças Implementadas

### 1. **Dano Físico e Mágico com Variação**

Antes:
```csharp
public int DanoFisico => (int)(Forca * 1.5f);      // Valor fixo
public int DanoMagico => (int)(Inteligencia * 1.5f); // Valor fixo
```

Depois:
```csharp
// Dano Físico: 8-12 base + bônus de Força
public int DanoFisicoMin => 8 + (Forca / 2);
public int DanoFisicoMax => 12 + (Forca / 2);
public string DanoFisico => $"{DanoFisicoMin}-{DanoFisicoMax}";  // Exibição: "10-14"

// Dano Mágico: 8-12 base + bônus de Inteligência  
public int DanoMagicoMin => 8 + (Inteligencia / 2);
public int DanoMagicoMax => 12 + (Inteligencia / 2);
public string DanoMagico => $"{DanoMagicoMin}-{DanoMagicoMax}";  // Exibição: "10-14"
```

**Exemplo com Força 20:**
- Dano Físico Min: 8 + 10 = **18**
- Dano Físico Max: 12 + 10 = **22**
- Cada ataque varia de **18 a 22 de dano**

---

### 2. **Destreza Focada em Crítico**

Antes:
```csharp
public float ChanceCritica => Destreza * 0.5f;
public float DanoCritico => 1.5f + (Destreza * 0.05f);  // Aumentava com Destreza!
public float Evasao => Destreza * 0.3f;
```

Depois:
```csharp
// Destreza aumenta APENAS Chance Crítica e Evasão
public float ChanceCritica => Destreza * 0.5f;   // 5% por ponto
public float Evasao => Destreza * 0.3f;          // 3% por ponto

// Dano Crítico fixo em 1.5x - APENAS itens aumentam
private float _bonusDanoCritico = 0f;
public float DanoCritico => 1.5f + _bonusDanoCritico;  // 1.5x base + itens
```

**Exemplo com Destreza 20:**
- Chance Crítica: 20 × 0.5 = **10%**
- Evasão: 20 × 0.3 = **6%**
- Dano Crítico: **1.5x** (sem itens)
- Dano Crítico: **2.0x** (com item +0.5x)

---

### 3. **Sistema de Bônus de Dano Crítico por Items**

Novos métodos em `EquipamentoComponent.cs`:

```csharp
/// Adiciona bônus de dano crítico de um item
public void AdicionarBonusDanoCritico(float bonusPercentual)
{
    _bonusDanoCritico += bonusPercentual;
    // Emite sinal: "🔥 Dano Crítico aumentado! Total: 2.0x"
}

/// Remove bônus ao desequipar
public void RemoverBonusDanoCritico(float bonusPercentual)
{
    _bonusDanoCritico -= bonusPercentual;
}
```

**Como usar em um item especial:**
```csharp
// Ao equipar uma "Adaga do Crítico" (+0.5x Dano Crítico)
equipamento.AdicionarBonusDanoCritico(0.5f);  // Dano Crítico agora = 2.0x

// Ao desequipar
equipamento.RemoverBonusDanoCritico(0.5f);   // Volta para 1.5x
```

---

### 4. **Dano com Variação no Projetil**

Antes:
```csharp
public int Dano = 10;  // Sempre 10
```

Depois:
```csharp
public int DanoMin = 8;              // Mínimo
public int DanoMax = 12;             // Máximo
public bool EhDanoMagico = false;    // Tipo de dano
```

**Novo método de cálculo:**
```csharp
private int CalcularDanoComCritico()
{
    // Dano variado: aleatório entre min e max
    int danoBase = (int)(GD.Randi() % (DanoMax - DanoMin + 1)) + DanoMin;
    
    // Verifica se é crítico (chance por Destreza)
    if (GD.Randf() * 100 < chanceCritica)
    {
        // Aplica multiplicador crítico
        return (int)(danoBase * multiplicadorCritico);
    }
    
    return danoBase;
}
```

---

## 🔢 Exemplos de Cálculo Completos

### Cenário 1: Ataques Normais vs Críticos

**Setup:**
- Força: 15 (DanoFisico 15-19)
- Destreza: 20 (10% Chance Crítica, 1.5x Dano Crítico)

**Ataque 1 - Não crítico:**
- Dano Base: 17 (aleatório entre 15-19)
- Chance: 85% (falhou crítico)
- **Dano Final: 17**

**Ataque 2 - Crítico:**
- Dano Base: 18 (aleatório entre 15-19)
- Chance: 10% (acertou crítico!)
- **Dano Final: 18 × 1.5 = 27**

---

### Cenário 2: Com Item de Crítico (+0.5x)

**Setup:**
- Força: 15 (DanoFisico 15-19)
- Destreza: 20 (10% Chance Crítica)
- Item Equipado: +0.5x Dano Crítico (Total: 2.0x)

**Ataque Crítico:**
- Dano Base: 17
- Chance: 10% (acertou!)
- **Dano Final: 17 × 2.0 = 34**

---

## 📊 Tabela de Progressão

| Atributo | Base | Pré Destreza | Pós Destreza |
|----------|------|-------------|------------|
| **Destreza 10** | - | ChanceCritica 5% | ✅ ChanceCritica 5% |
| **Destreza 10** | - | DanoCritico 2.0x | ✅ DanoCritico 1.5x |
| **Destreza 20** | - | ChanceCritica 10% | ✅ ChanceCritica 10% |
| **Destreza 20** | - | DanoCritico 2.5x | ✅ DanoCritico 1.5x |

**Conclusão:** Destreza agora afeta APENAS Chance Crítica e Evasão. Dano Crítico depende de itens!

---

## 🎮 Como Usar em Equipamentos

### Armadura de Crítico (+0.3x Dano Crítico)

```csharp
public void Equipar()
{
    equipamentoComponent.AdicionarBonusDanoCritico(0.3f);
    // Dano Crítico: 1.5 + 0.3 = 1.8x
}

public void Desequipar()
{
    equipamentoComponent.RemoverBonusDanoCritico(0.3f);
    // Dano Crítico: volta para 1.5x
}
```

### Adaga Assassina (+0.5x Dano Crítico)

```csharp
public void Equipar()
{
    equipamentoComponent.AdicionarBonusDanoCritico(0.5f);
    // Dano Crítico: 1.5 + 0.5 = 2.0x
}
```

### Anel do Crítico Extremo (+0.8x Dano Crítico)

```csharp
public void Equipar()
{
    equipamentoComponent.AdicionarBonusDanoCritico(0.8f);
    // Dano Crítico: 1.5 + 0.8 = 2.3x - Crítico devastador!
}
```

---

## 📲 Feedback no Console

**Dano Normal:**
```
💥 Projétil acertou Goblin! 17 de dano (entre 15-19).
```

**Dano Crítico:**
```
⚡ GOLPE CRÍTICO! 27 de dano em Goblin!
```

**Equipando item especial:**
```
🔥 Dano Crítico aumentado! Novo bônus: +0.5x (Total: 2.0x)
```

**Desequipando:**
```
📉 Dano Crítico reduzido! Novo bônus: +0.0x (Total: 1.5x)
```

---

## ✅ Validações

- ✅ Compilação bem-sucedida
- ✅ Sem erros de tipo (long → int corrigido)
- ✅ Dano variado funcionando (8-12 base)
- ✅ Destreza afeta apenas ChanceCritica e Evasao
- ✅ Dano Crítico base em 1.5x fixo
- ✅ Sistema de bônus de itens funcionando
- ✅ Feedback visual diferenciado

---

## 🎲 Próximas Melhorias Opcionais

1. **Items com bônus variável**
   - Item comum: +0.2x Dano Crítico
   - Item raro: +0.5x Dano Crítico
   - Item lendário: +1.0x Dano Crítico

2. **Combos de itens**
   - "Conjunto de Assassino": Se equipar 3 peças, +0.3x extra

3. **Statues especiais**
   - Crítico não pode cair abaixo de 1.5x
   - Máximo recomendado: 3.5-4.0x (balanceamento)

4. **Efeitos especiais**
   - Crítico causa bleed/veneno
   - Crítico restaura mana
   - Crítico aumenta próximas chances

---

## 📁 Arquivos Atualizados

- [characterSlots/EquipamentoComponent.cs](characterSlots/EquipamentoComponent.cs)
  - Nova propriedade `_bonusDanoCritico`
  - Novos métodos `AdicionarBonusDanoCritico()` e `RemoverBonusDanoCritico()`
  - Novos métodos `CalcularDanoFisicoAleatorio()` e `CalcularDanoMagicoAleatorio()`
  - DanoFisico e DanoMagico agora retornam strings "min-max"

- [resources/Projetil/Projetil.cs](resources/Projetil/Projetil.cs)
  - Novo campo `DanoMin` e `DanoMax`
  - Novo campo `EhDanoMagico`
  - Método `CalcularDanoComCritico()` atualizado com variação
  - Detecção de crítico melhorada

- [characterSlots/CharacterUI.cs](characterSlots/CharacterUI.cs)
  - Exibição de dano em formato "min-max" funcionando automaticamente

---

## 🚀 Resumo Rápido

| Função | Antes | Depois |
|--------|-------|--------|
| **Dano Físico** | Fixo (ex: 15) | Variado (ex: 13-17) |
| **Dano Mágico** | Fixo (ex: 15) | Variado (ex: 13-17) |
| **Destreza → Crítico** | +0.05x/ponto | ❌ Removido |
| **Destreza → Chance** | +0.5%/ponto | ✅ Mantido |
| **Crítico Base** | Variável com Destreza | 🔒 Fixo 1.5x |
| **Aumentar Crítico** | Aumentar Destreza | 🎁 Equipar itens especiais |

