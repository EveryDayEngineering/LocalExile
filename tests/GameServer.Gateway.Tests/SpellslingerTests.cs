using GameServer.Core.Protocol.Instance;

namespace GameServer.Gateway.Tests;

// Several Spellslingers run side by side, as live: each reserves for its own linked spells, keeps its own buff instance
// (same row, same source, told apart by its skill id as tag) and comes back from the save.
public sealed class SpellslingerTests
{
    [Fact]
    public async Task Two_spellslingers_start_publish_their_own_buffs_and_restore_together()
    {
        using var f = new TriggeredSummonTests.Fixture();
        var player = f.Player;
        uint id = Game.ItemsOf(player).Items(PlayerInventories.Weapon1).Single().Id;
        Game.ItemsOf(player).Change(PlayerInventories.Weapon1, id, item => item with
        {
            Sockets = new[] { "Spellslinger", "Fireball", "Spellslinger", "Arc" }.Select(n =>
            {
                var type = Program.Items.FindGem(n)!.Value;
                return new ItemSocket(SocketColour.White, ItemCatalog.Create(type.Type) with
                {
                    GemVariant = type.Variant.Index, GemExperience = Program.Items.Gem(type.Type)!.ExperienceFor(20)
                });
            }).ToArray(),
            Links = [2, 2],
        });
        var slingers = Game.GemSkillEffects(player).Where(g => Program.Skills.EffectId(g.Effect) == "Spellslinger").ToArray();
        Assert.Equal(2, slingers.Length);

        foreach (var gem in slingers)
            Assert.True(await Game.ToggleAura(player, gem.Id, gem.Effect, gem.Name,
                Game.SupportedReservationCost(player, gem.Id)!.Value, gem.Level, gem.Quality));
        var auras = player.Auras.Values.Where(a => a.EffectId == "Spellslinger").ToArray();
        Assert.Equal(2, auras.Length);

        // Publishing both buffs used to throw on the duplicate key (same row and source).
        Game.WithOwnAuraRecords(player, () => []);
        ushort row = AuraCatalog.BuffRow("Spellslinger");
        foreach (var gem in slingers) Assert.True(player.Area.Buffs.Has(player.Id, row, player.Id, tag: gem.Id));

        // A saved Spellslinger brings back every one that fits.
        Assert.Equal(2, AuraCatalog.Restore(["Spellslinger"], auras, player.Pools).Count);
    }
}
