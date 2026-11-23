using Munchkin.Api.Models;

namespace Munchkin.Api.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext context)
    {
        if (context.CardPacks.Any()) return; // Si ya hay datos, no hace nada

        var basePack = new CardPack
        {
            Name = "Mazo Base (Prueba)",
            Description = "Cartas iniciales para probar el motor",
            IsOfficial = true
        };

        context.CardPacks.Add(basePack);
        context.SaveChanges();

        var cards = new List<Card>
        {
            // --- 10 PUERTAS (DOORS) ---
            new() { PackId = basePack.Id, Name = "Planta en Maceta", DeckType = "door", CardType = "monster", Description = "Los Elfos roban un tesoro extra.", Stats = """{"level": 1, "treasures": 1, "levelsGained": 1, "badStuff": "Ninguno. Huir es automático."}""" },
            new() { PackId = basePack.Id, Name = "Goblin Mutilado", DeckType = "door", CardType = "monster", Description = "+1 para huir.", Stats = """{"level": 1, "treasures": 1, "levelsGained": 1, "badStuff": "Te pega con su muleta. Pierdes 1 nivel."}""" },
            new() { PackId = basePack.Id, Name = "Orcos 3872", DeckType = "door", CardType = "monster", Description = "+6 contra Enanos.", Stats = """{"level": 10, "treasures": 3, "levelsGained": 1, "badStuff": "Tira un dado. Con 2 o menos mueres; si no, pierdes tantos niveles como indique el dado."}""" },
            new() { PackId = basePack.Id, Name = "Dragón de Plutonio", DeckType = "door", CardType = "monster", Description = "No persigue a nadie de Nivel 5 o inferior.", Stats = """{"level": 20, "treasures": 5, "levelsGained": 2, "badStuff": "Te achicharra y mueres."}""" },
            new() { PackId = basePack.Id, Name = "Ladrón", DeckType = "door", CardType = "class", Description = "Descarta 1 carta para intentar robar (1 intento por jugador/turno). Tira dado: 4 o menos pierdes 1 nivel.", Stats = """{"class": "Ladrón"}""" },
            new() { PackId = basePack.Id, Name = "Guerrero", DeckType = "door", CardType = "class", Description = "Ganas en caso de empate en combate. Descarta hasta 3 cartas para +1 cada una.", Stats = """{"class": "Guerrero"}""" },
            new() { PackId = basePack.Id, Name = "Elfo", DeckType = "door", CardType = "race", Description = "+1 para huir. Subes 1 nivel por cada monstruo que ayudes a matar.", Stats = """{"race": "Elfo"}""" },
            new() { PackId = basePack.Id, Name = "Enano", DeckType = "door", CardType = "race", Description = "Puedes llevar cualquier número de objetos Grandes y hasta 6 cartas en mano.", Stats = """{"race": "Enano"}""" },
            new() { PackId = basePack.Id, Name = "Enorme", DeckType = "door", CardType = "monster_enhancer", Description = "+10 al monstruo. Si es derrotado, roba 2 tesoros extra.", Stats = """{"combatBonus": 10, "extraTreasures": 2}""" },
            new() { PackId = basePack.Id, Name = "¡Maldición! Pierdes 1 Nivel", DeckType = "door", CardType = "curse", Description = "Pierdes 1 nivel inmediatamente.", Stats = """{"levelDelta": -1}""" },

            // --- 10 TESOROS (TREASURES) ---
            new() { PackId = basePack.Id, Name = "Espada Bastarda", DeckType = "treasure", CardType = "item", Description = "Arma clásica de 1 mano.", Stats = """{"bonus": 3, "slot": "1_hand", "isBig": false, "goldValue": 400}""" },
            new() { PackId = basePack.Id, Name = "Yelmo Cornudo", DeckType = "treasure", CardType = "item", Description = "+1 normal (+3 para Elfos).", Stats = """{"bonus": 1, "slot": "headgear", "isBig": false, "goldValue": 600}""" },
            new() { PackId = basePack.Id, Name = "Armadura Llameante", DeckType = "treasure", CardType = "item", Description = "Te ves increíble, pero hace calor.", Stats = """{"bonus": 2, "slot": "armor", "isBig": false, "goldValue": 400}""" },
            new() { PackId = basePack.Id, Name = "Botas de Correr Muy Rápido", DeckType = "treasure", CardType = "item", Description = "Te dan +2 para huir.", Stats = """{"bonus": 0, "slot": "footgear", "isBig": false, "goldValue": 400}""" },
            new() { PackId = basePack.Id, Name = "Motosierra de Mutilación Sangrienta", DeckType = "treasure", CardType = "item", Description = "Objeto Grande de 2 manos.", Stats = """{"bonus": 3, "slot": "2_hands", "isBig": true, "goldValue": 600}""" },
            new() { PackId = basePack.Id, Name = "Poción de Hielo", DeckType = "treasure", CardType = "one_shot", Description = "Úsala en cualquier combate. +3 a cualquier bando.", Stats = """{"combatBonus": 3, "target": "any", "goldValue": 100}""" },
            new() { PackId = basePack.Id, Name = "Poción de Llamas", DeckType = "treasure", CardType = "one_shot", Description = "Úsala en cualquier combate. +2 a cualquier bando.", Stats = """{"combatBonus": 2, "target": "any", "goldValue": 100}""" },
            new() { PackId = basePack.Id, Name = "Anillo de los Deseos", DeckType = "treasure", CardType = "one_shot", Description = "Cancela cualquier maldición. Un solo uso.", Stats = """{"goldValue": 500}""" },
            new() { PackId = basePack.Id, Name = "Sobornar al Máster con Comida", DeckType = "treasure", CardType = "level_up", Description = "Sube 1 nivel (no sirve para llegar al 10).", Stats = """{"levelDelta": 1}""" },
            new() { PackId = basePack.Id, Name = "Mutilar los Cadáveres", DeckType = "treasure", CardType = "level_up", Description = "Sube 1 nivel (no sirve para llegar al 10).", Stats = """{"levelDelta": 1}""" }
        };

        context.Cards.AddRange(cards);
        context.SaveChanges();
    }
}