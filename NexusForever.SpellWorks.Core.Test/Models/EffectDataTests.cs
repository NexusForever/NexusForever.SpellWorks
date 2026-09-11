using Microsoft.Extensions.DependencyInjection;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Models.Effect;
using Xunit;

namespace NexusForever.SpellWorks.Core.Test.Models
{
    /// <summary>
    /// How an effect's ten data columns are labelled and rendered. A generic effect shows raw bits; the
    /// effect types that know better reinterpret them.
    /// </summary>
    public class EffectDataTests
    {
        private static Spell4EffectsEntry Entry() => new()
        {
            DataBits00 = 0,
            DataBits01 = 11,
            DataBits02 = 22,
            DataBits03 = 33,
            DataBits04 = 44,
            DataBits05 = 55,
            DataBits06 = 66,
            DataBits07 = 77,
            DataBits08 = 88,
            DataBits09 = 99
        };

        [Fact]
        public void A_generic_effect_shows_the_raw_bits()
        {
            var data = new DefaultSpellEffectRowData { Entry = Entry() };

            Assert.Equal("0", data.Data00);
            Assert.Equal("11", data.Data01);
            Assert.Equal("22", data.Data02);
            Assert.Equal("33", data.Data03);
            Assert.Equal("44", data.Data04);
            Assert.Equal("55", data.Data05);
            Assert.Equal("66", data.Data06);
            Assert.Equal("77", data.Data07);
            Assert.Equal("88", data.Data08);
            Assert.Equal("99", data.Data09);
        }

        [Fact]
        public void A_generic_effect_cross_references_nothing()
        {
            Assert.Empty(new DefaultSpellEffectRowData { Entry = Entry() }.HyperlinkedDataIndices);
        }

        [Fact]
        public void Every_column_can_be_read_back_as_the_raw_value_behind_it()
        {
            var data = new DefaultSpellEffectRowData { Entry = Entry() };

            uint[] expected =
            [
                data.Entry.DataBits00, data.Entry.DataBits01, data.Entry.DataBits02, data.Entry.DataBits03,
                data.Entry.DataBits04, data.Entry.DataBits05, data.Entry.DataBits06, data.Entry.DataBits07,
                data.Entry.DataBits08, data.Entry.DataBits09
            ];

            Assert.Equal(expected, Enumerable.Range(0, 10).Select(data.DataBits));
            Assert.Throws<ArgumentOutOfRangeException>(() => data.DataBits(10));
            Assert.Throws<ArgumentOutOfRangeException>(() => data.DataBits(-1));
        }

        [Fact]
        public void A_generic_effect_labels_its_columns_by_position()
        {
            var columns = new DefaultSpellEffectColumnData();

            Assert.Equal("Data00", columns.Data00ColumnName);
            Assert.Equal("Data01", columns.Data01ColumnName);
            Assert.Equal("Data02", columns.Data02ColumnName);
            Assert.Equal("Data03", columns.Data03ColumnName);
            Assert.Equal("Data04", columns.Data04ColumnName);
            Assert.Equal("Data05", columns.Data05ColumnName);
            Assert.Equal("Data06", columns.Data06ColumnName);
            Assert.Equal("Data07", columns.Data07ColumnName);
            Assert.Equal("Data08", columns.Data08ColumnName);
            Assert.Equal("Data09", columns.Data09ColumnName);
        }

        [Fact]
        public void Damage_reads_its_first_column_as_a_float()
        {
            // The column holds the bits of a single, not a number to be printed as-is.
            uint bits = BitConverter.SingleToUInt32Bits(12.5f);
            var data = new DamageSpellEffectRowData { Entry = new Spell4EffectsEntry { DataBits00 = bits } };

            Assert.Equal(12.5f.ToString(), data.Data00);
            Assert.Empty(data.HyperlinkedDataIndices);
        }

        [Fact]
        public void Heal_reads_its_first_column_as_a_float_too()
        {
            uint bits = BitConverter.SingleToUInt32Bits(-7.25f);
            var data = new HealSpellEffectRowData { Entry = new Spell4EffectsEntry { DataBits00 = bits } };

            Assert.Equal((-7.25f).ToString(), data.Data00);
        }

        [Fact]
        public void A_proxy_links_the_columns_that_name_the_spells_it_casts()
        {
            // A proxy names up to three spells, in its first three Data columns; Sprint (1316) leaves the
            // first empty and carries its proxied spell in the second, so all three have to be offered.
            var data = new ProxySpellEffectRowData
            {
                Entry = new Spell4EffectsEntry { DataBits00 = 0, DataBits01 = 40931, DataBits02 = 555 }
            };

            Assert.Equal([0, 1, 2], data.HyperlinkedDataIndices);
            Assert.Equal(0u, data.DataBits(0));
            Assert.Equal(40931u, data.DataBits(1));
            Assert.Equal(555u, data.DataBits(2));
        }

        [Fact]
        public void A_vital_modifier_names_the_vital_it_changes()
        {
            var data = new VitalModifierSpellEffectRowData
            {
                Entry = new Spell4EffectsEntry { DataBits00 = (uint)Vital.Health }
            };

            Assert.Equal($"{(uint)Vital.Health} - {nameof(Vital.Health)}", data.Data00);
        }

        [Fact]
        public void An_unknown_vital_still_shows_its_number()
        {
            var data = new VitalModifierSpellEffectRowData
            {
                Entry = new Spell4EffectsEntry { DataBits00 = 9999 }
            };

            Assert.Equal("9999 - ", data.Data00);
        }

        [Fact]
        public void A_vital_modifier_labels_its_first_column_Vital()
        {
            var columns = new VitalModifierSpellEffectColumnData();

            Assert.Equal("Vital", columns.Data00ColumnName);
            Assert.Equal("Data01", columns.Data01ColumnName);
        }

        [Fact]
        public void An_effect_type_is_bound_to_its_projection_by_attribute()
        {
            var effect = typeof(DamageSpellEffectRowData)
                .GetCustomAttributes(typeof(SpellEffectAttribute), false)
                .Cast<SpellEffectAttribute>()
                .Single();

            Assert.Equal(SpellEffectType.Damage, effect.Type);
        }

        [Fact]
        public void The_container_resolves_the_projection_registered_for_an_effect_type()
        {
            ServiceProvider provider = new ServiceCollection().AddSpellEffectData().BuildServiceProvider();

            Assert.IsType<DamageSpellEffectRowData>(
                provider.GetKeyedService<ISpellEffectRowData>(SpellEffectType.Damage));
            Assert.IsType<HealSpellEffectRowData>(
                provider.GetKeyedService<ISpellEffectRowData>(SpellEffectType.Heal));
            Assert.IsType<ProxySpellEffectRowData>(
                provider.GetKeyedService<ISpellEffectRowData>(SpellEffectType.Proxy));
            Assert.IsType<VitalModifierSpellEffectColumnData>(
                provider.GetKeyedService<ISpellEffectColumnData>(SpellEffectType.VitalModifier));
        }

        [Fact]
        public void An_effect_type_with_no_projection_resolves_to_nothing()
        {
            ServiceProvider provider = new ServiceCollection().AddSpellEffectData().BuildServiceProvider();

            Assert.Null(provider.GetKeyedService<ISpellEffectRowData>((SpellEffectType)9999));
        }
    }
}
