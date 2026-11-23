using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualBasic.FileIO;
using Munchkin.Api.Data;
using Munchkin.Api.Models;
using System.Text.Json;

namespace Munchkin.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CardsController : ControllerBase
{
    private readonly AppDbContext _db;

    public CardsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult GetAllCards()
    {
        return Ok(_db.Cards.ToList());
    }

    [HttpPost("import-csv")]
    public async Task<IActionResult> ImportCsv(IFormFile file, [FromQuery] bool replaceAll = false)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No se subió ningún archivo CSV.");

        if (replaceAll)
        {
            _db.Cards.RemoveRange(_db.Cards);
            await _db.SaveChangesAsync();
        }

        int importedCount = 0;
        int skippedCount = 0;

        using var stream = file.OpenReadStream();
        using var parser = new TextFieldParser(stream);
        parser.TextFieldType = FieldType.Delimited;
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;

        // Saltar la fila 1 (encabezados)
        if (!parser.EndOfData) parser.ReadFields();

        while (!parser.EndOfData)
        {
            var cols = parser.ReadFields();
            if (cols == null || cols.Length < 5) continue;

            string packName = cols[0].Trim();
            string name = cols[1].Trim();
            string deckType = cols[2].Trim().ToLower();
            string cardType = cols[3].Trim().ToLower();
            string description = cols[4].Trim();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(deckType) || string.IsNullOrWhiteSpace(cardType))
            {
                skippedCount++;
                continue;
            }

            // Buscar o crear el Pack automáticamente
            if (string.IsNullOrWhiteSpace(packName)) packName = "Mazo Base";
            var pack = _db.CardPacks.FirstOrDefault(p => p.Name == packName);
            if (pack == null)
            {
                pack = new CardPack { Name = packName, IsOfficial = packName != "Mazo Amigos" };
                _db.CardPacks.Add(pack);
                await _db.SaveChangesAsync();
            }

            // Evitar duplicados por nombre si no estamos reemplazando todo
            if (!replaceAll && _db.Cards.Any(c => c.Name == name && c.PackId == pack.Id))
            {
                skippedCount++;
                continue;
            }

            // Armar el JSONB de stats según las columnas del Google Sheets
            var stats = new Dictionary<string, object>();

            int? GetInt(int index) =>
                index < cols.Length && int.TryParse(cols[index].Trim(), out var val) ? val : null;
            string GetStr(int index) =>
                index < cols.Length ? cols[index].Trim() : string.Empty;

            if (cardType == "monster")
            {
                if (GetInt(5) is int lvl) stats["level"] = lvl;
                if (GetInt(6) is int trs) stats["treasures"] = trs;
                stats["levelsGained"] = GetInt(7) ?? 1;
                if (!string.IsNullOrEmpty(GetStr(8))) stats["badStuff"] = GetStr(8);
            }
            else if (cardType == "item")
            {
                if (GetInt(9) is int bonus) stats["bonus"] = bonus;
                if (!string.IsNullOrEmpty(GetStr(10))) stats["slot"] = GetStr(10).ToLower();
                stats["isBig"] = GetStr(11).Equals("SI", StringComparison.OrdinalIgnoreCase);
                if (GetInt(12) is int gold) stats["goldValue"] = gold;
            }
            else if (cardType == "one_shot")
            {
                if (GetInt(9) is int cBonus) stats["combatBonus"] = cBonus;
                if (GetInt(12) is int gold) stats["goldValue"] = gold;
                if (!string.IsNullOrEmpty(GetStr(13))) stats["target"] = GetStr(13).ToLower();
            }
            else if (cardType == "monster_enhancer")
            {
                if (GetInt(9) is int cBonus) stats["combatBonus"] = cBonus;
                if (GetInt(6) is int extraTrs) stats["extraTreasures"] = extraTrs;
            }
            else if (cardType == "level_up")
            {
                stats["levelDelta"] = 1;
            }
            else if (cardType == "curse")
            {
                if (GetInt(9) is int delta) stats["levelDelta"] = delta;
            }
            else if (cardType == "race")
            {
                stats["race"] = name;
            }
            else if (cardType == "class")
            {
                stats["class"] = name;
            }

            _db.Cards.Add(new Card
            {
                PackId = pack.Id,
                Name = name,
                DeckType = deckType,
                CardType = cardType,
                Description = description,
                Stats = JsonSerializer.Serialize(stats)
            });

            importedCount++;
        }

        await _db.SaveChangesAsync();
        return Ok(new { imported = importedCount, skipped = skippedCount });
    }
}