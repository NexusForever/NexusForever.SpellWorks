using NexusForever.GameTable;
using NexusForever.GameTable.Model;
using Xunit;

namespace NexusForever.SpellWorks.Core.Test.Testing
{
    /// <summary>
    /// The writer is a test fixture, so it gets its own tests: every other game-table test trusts it.
    /// </summary>
    public class GameTableWriterTests
    {
        [Fact]
        public void Round_trips_numeric_columns()
        {
            GameTable<Spell4Entry> table = GameTableWriter.Table(
                new Spell4Entry { Id = 1, Spell4BaseIdBaseSpell = 10, TierIndex = 3 },
                new Spell4Entry { Id = 2, Spell4BaseIdBaseSpell = 20, TierIndex = 5 });

            Assert.Equal(2, table.Entries.Length);
            Assert.Equal(1u, table.Entries[0].Id);
            Assert.Equal(10u, table.Entries[0].Spell4BaseIdBaseSpell);
            Assert.Equal(3u, table.Entries[0].TierIndex);
            Assert.Equal(20u, table.Entries[1].Spell4BaseIdBaseSpell);
            Assert.Equal(5u, table.Entries[1].TierIndex);
        }

        [Fact]
        public void Round_trips_string_columns()
        {
            GameTable<Spell4Entry> table = GameTableWriter.Table(
                new Spell4Entry { Id = 1, Description = "Arcane Missile" },
                new Spell4Entry { Id = 2, Description = "" });

            Assert.Equal("Arcane Missile", table.Entries[0].Description);
            Assert.Equal("", table.Entries[1].Description);
        }

        [Fact]
        public void Round_trips_float_and_array_columns()
        {
            GameTable<Spell4EffectsEntry> table = GameTableWriter.Table(
                new Spell4EffectsEntry
                {
                    Id                = 7,
                    SpellId           = 42,
                    DataBits00        = 99,
                    DelayTime         = 250,
                    ParameterValue    = [1.5f, 2.5f, 0f, 0f]
                });

            Spell4EffectsEntry entry = table.Entries[0];

            Assert.Equal(42u, entry.SpellId);
            Assert.Equal(99u, entry.DataBits00);
            Assert.Equal(250u, entry.DelayTime);
            Assert.Equal(1.5f, entry.ParameterValue[0]);
            Assert.Equal(2.5f, entry.ParameterValue[1]);
        }

        [Fact]
        public void Builds_a_lookup_that_finds_entries_by_id()
        {
            GameTable<Spell4BaseEntry> table = GameTableWriter.Table(
                new Spell4BaseEntry { Id = 4, LocalizedTextIdName = 100 },
                new Spell4BaseEntry { Id = 9, LocalizedTextIdName = 200 });

            Assert.Equal(100u, table.GetEntry(4).LocalizedTextIdName);
            Assert.Equal(200u, table.GetEntry(9).LocalizedTextIdName);
            Assert.Null(table.GetEntry(5));
        }

        [Fact]
        public void Writes_an_empty_table()
        {
            GameTable<Spell4Entry> table = GameTableWriter.Table<Spell4Entry>();

            Assert.Empty(table.Entries);
        }
    }
}
