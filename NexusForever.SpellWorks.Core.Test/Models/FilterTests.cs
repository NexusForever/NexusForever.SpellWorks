using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Spell;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Models.Filter;
using NexusForever.SpellWorks.Core.Static;
using NexusForever.SpellWorks.Core.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Core.Test.Models
{
    /// <summary>
    /// Every filter is a pure predicate over one spell, so each is driven with a matching and a
    /// non-matching spell plus whatever edge case its own code carves out.
    /// </summary>
    public class FilterTests
    {
        [Fact]
        public void The_text_search_matches_the_description_case_insensitively()
        {
            var filter = new SpellModelTextSearchFilter { Query = "missile" };

            Assert.True(filter.Filter(SpellModelBuilder.A(1, "Arcane MISSILE").Build()));
            Assert.False(filter.Filter(SpellModelBuilder.A(2, "Healing Wave").Build()));
        }

        [Fact]
        public void The_text_search_does_not_match_the_id()
        {
            // The id has its own box, so a typed number is not also matched against every description.
            var filter = new SpellModelTextSearchFilter { Query = "234" };

            Assert.False(filter.Filter(SpellModelBuilder.A(12345, "").Build()));
            Assert.True(new SpellModelIdSearchFilter { Query = "234" }
                .Filter(SpellModelBuilder.A(12345, "").Build()));
        }

        [Fact]
        public void The_id_search_matches_a_substring_or_the_whole_id()
        {
            Assert.True(new SpellModelIdSearchFilter { Query = "234" }
                .Filter(SpellModelBuilder.A(12345).Build()));
            Assert.False(new SpellModelIdSearchFilter { Query = "234", Exact = true }
                .Filter(SpellModelBuilder.A(12345).Build()));
            Assert.True(new SpellModelIdSearchFilter { Query = "12345", Exact = true }
                .Filter(SpellModelBuilder.A(12345).Build()));
        }

        [Fact]
        public void An_exact_text_search_matches_the_whole_description()
        {
            var filter = new SpellModelTextSearchFilter { Query = "Arcane Missile", Exact = true };

            Assert.True(filter.Filter(SpellModelBuilder.A(1, "arcane missile").Build()));
            Assert.False(filter.Filter(SpellModelBuilder.A(2, "Arcane Missile II").Build()));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void An_empty_search_keeps_everything(string query)
        {
            Assert.True(new SpellModelTextSearchFilter { Query = query }
                .Filter(SpellModelBuilder.A(1, "anything").Build()));
            Assert.True(new SpellModelIdSearchFilter { Query = query }
                .Filter(SpellModelBuilder.A(1, "anything").Build()));
        }

        [Fact]
        public void Search_tolerates_a_spell_with_no_description()
        {
            var filter = new SpellModelTextSearchFilter { Query = "missile" };

            Assert.False(filter.Filter(SpellModelBuilder.A(1).Description(null).Build()));
        }

        [Fact]
        public void Id_filter_matches_on_a_prefix_not_a_substring()
        {
            var filter = new SpellModelIdFilter { IdPrefix = "123" };

            Assert.True(filter.Filter(SpellModelBuilder.A(12345).Build()));
            Assert.False(filter.Filter(SpellModelBuilder.A(51234).Build()));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void An_empty_id_prefix_keeps_everything(string prefix)
        {
            var filter = new SpellModelIdFilter { IdPrefix = prefix };

            Assert.True(filter.Filter(SpellModelBuilder.A(7).Build()));
        }

        [Fact]
        public void Description_filter_matches_a_substring()
        {
            var filter = new SpellModelDescriptionFilter { Description = "wave" };

            Assert.True(filter.Filter(SpellModelBuilder.A(1, "Healing Wave").Build()));
            Assert.False(filter.Filter(SpellModelBuilder.A(2, "Arcane Missile").Build()));
        }

        [Fact]
        public void A_null_description_filter_keeps_everything()
        {
            var filter = new SpellModelDescriptionFilter { Description = null };

            Assert.True(filter.Filter(SpellModelBuilder.A(1, "anything").Build()));
        }

        [Theory]
        [InlineData("[DEPRECATED] Old spell", true)]
        [InlineData("Deprecate - do not use", true)]
        [InlineData("deprecated in a later patch", true)]
        [InlineData("Arcane Missile", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void Deprecation_is_read_out_of_the_description(string description, bool deprecated)
        {
            // Spell4 carries no deprecation column; the client data flags it in the description.
            ISpellModel model = SpellModelBuilder.A(1, description).Build();

            Assert.Equal(deprecated, SpellModelDeprecatedFilter.IsDeprecated(model));

            // The filter is phrased positively - it selects deprecated spells. "Hide deprecated" is this
            // filter negated, which is what NotFilter is for.
            Assert.Equal(deprecated, new SpellModelDeprecatedFilter().Filter(model));
            Assert.Equal(!deprecated, new NotFilter<ISpellModel>(new SpellModelDeprecatedFilter()).Filter(model));
        }

        [Theory]
        [InlineData("[Test] Placeholder", true)]
        [InlineData("[TEST] shouting", true)]
        [InlineData("something [test] mid-sentence", true)]
        [InlineData("Testing the waters", false)]
        [InlineData("Contest of champions", false)]
        [InlineData("Arcane Missile", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void A_test_spell_is_read_out_of_the_description(string description, bool test)
        {
            // The bracketed marker and nothing looser: "test" as a bare word turns up in real spell names,
            // and a housekeeping filter that hides those is worse than no filter at all.
            ISpellModel model = SpellModelBuilder.A(1, description).Build();

            Assert.Equal(test, SpellModelTestFilter.IsTest(model));

            // Phrased positively, as deprecation is - it selects the test spells, and "hide them" is this
            // filter negated.
            Assert.Equal(test, new SpellModelTestFilter().Filter(model));
            Assert.Equal(!test, new NotFilter<ISpellModel>(new SpellModelTestFilter()).Filter(model));
        }

        [Fact]
        public void Cast_method_matches_the_base_entry()
        {
            var filter = new SpellModelCastMethodFilter { CastMethod = CastMethod.Channeled };

            Assert.True(filter.Filter(SpellModelBuilder.A().CastMethod(CastMethod.Channeled).Build()));
            Assert.False(filter.Filter(SpellModelBuilder.A().CastMethod(CastMethod.Normal).Build()));
        }

        [Fact]
        public void Class_matches_the_base_entry()
        {
            var filter = new SpellModelClassFilter { Class = Class.Esper };

            Assert.True(filter.Filter(SpellModelBuilder.A().Class((uint)Class.Esper).Build()));
            Assert.False(filter.Filter(SpellModelBuilder.A().Class((uint)Class.Warrior).Build()));
        }

        [Fact]
        public void School_matches_the_base_entry()
        {
            var filter = new SpellModelSchoolFilter { School = DamageType.Magic };

            Assert.True(filter.Filter(SpellModelBuilder.A().School((uint)DamageType.Magic).Build()));
            Assert.False(filter.Filter(SpellModelBuilder.A().School((uint)DamageType.Physical).Build()));
        }

        [Fact]
        public void Effect_type_matches_when_any_effect_has_it()
        {
            var filter = new SpellModelEffectTypeFilter { SpellEffectType = SpellEffectType.Heal };

            Assert.True(filter.Filter(SpellModelBuilder.A()
                .Effect(SpellEffectType.Damage)
                .Effect(SpellEffectType.Heal)
                .Build()));

            Assert.False(filter.Filter(SpellModelBuilder.A().Effect(SpellEffectType.Damage).Build()));
            Assert.False(filter.Filter(SpellModelBuilder.A().Build()));
        }

        [Fact]
        public void Effect_target_flags_require_every_requested_bit()
        {
            var filter = new SpellModelEffectTargetFlagsFilter { Flags = 0b0110 };

            Assert.True(filter.Filter(SpellModelBuilder.A().Effect(SpellEffectType.Damage, 0b1110).Build()));
            Assert.False(filter.Filter(SpellModelBuilder.A().Effect(SpellEffectType.Damage, 0b0100).Build()));
        }

        [Fact]
        public void Effect_target_flags_of_zero_keep_everything()
        {
            var filter = new SpellModelEffectTargetFlagsFilter { Flags = 0 };

            Assert.True(filter.Filter(SpellModelBuilder.A().Build()));
        }

        [Fact]
        public void Target_mechanic_flags_require_every_requested_bit()
        {
            var filter = new SpellModelTargetMechanicFlagsFilter { Flags = 0b0011 };

            Assert.True(filter.Filter(SpellModelBuilder.A().TargetMechanic(0, 0b1011).Build()));
            Assert.False(filter.Filter(SpellModelBuilder.A().TargetMechanic(0, 0b0010).Build()));
        }

        [Fact]
        public void Target_mechanic_flags_of_zero_keep_everything()
        {
            var filter = new SpellModelTargetMechanicFlagsFilter { Flags = 0 };

            Assert.True(filter.Filter(SpellModelBuilder.A().Build()));
        }

        [Fact]
        public void Target_mechanic_type_matches_the_mechanic_entry()
        {
            var filter = new SpellModelTargetMechanicTypeFilter
            {
                TargetMechanicType = (SpellTargetMechanicType)2
            };

            Assert.True(filter.Filter(SpellModelBuilder.A().TargetMechanic(2, 0).Build()));
            Assert.False(filter.Filter(SpellModelBuilder.A().TargetMechanic(3, 0).Build()));
        }

        [Fact]
        public void Has_procs_keeps_only_spells_that_cast_one()
        {
            var filter = new SpellModelHasProcsFilter();

            Assert.True(filter.Filter(SpellModelBuilder.A().Proc(42).Build()));
            Assert.False(filter.Filter(SpellModelBuilder.A().Build()));
        }

        [Fact]
        public void Proc_referenced_keeps_only_spells_another_proc_points_at()
        {
            var filter = new SpellModelProcReferencedFilter();

            Assert.True(filter.Filter(SpellModelBuilder.A().ReferencedByProc(7).Build()));
            Assert.False(filter.Filter(SpellModelBuilder.A().Build()));
        }
    
        [Fact]
        public void The_base_row_filters_tolerate_a_spell_with_no_base_row()
        {
            // Spell4Base is a join, and the join misses on malformed data. A spell with no base row has no
            // cast method, school or class - so it matches none of them rather than throwing.
            ISpellModel orphan = SpellModelBuilder.A(1).NoBase().Build();

            Assert.False(new SpellModelCastMethodFilter { CastMethod = CastMethod.Channeled }.Filter(orphan));
            Assert.False(new SpellModelSchoolFilter { School = DamageType.Magic }.Filter(orphan));
            Assert.False(new SpellModelClassFilter { Class = Class.Esper }.Filter(orphan));
            Assert.False(new SpellModelTargetMechanicTypeFilter().Filter(orphan));
        }

        [Fact]
        public void The_mechanic_type_filter_tolerates_a_base_row_with_no_mechanics()
        {
            ISpellModel spell = SpellModelBuilder.A(1).NoMechanics().Build();

            Assert.False(new SpellModelTargetMechanicTypeFilter().Filter(spell));
        }

        [Fact]
        public void The_description_filter_tolerates_a_spell_with_no_description()
        {
            var filter = new SpellModelDescriptionFilter { Description = "missile" };

            Assert.False(filter.Filter(SpellModelBuilder.A(1).Description(null).Build()));
            Assert.True(new SpellModelDescriptionFilter().Filter(SpellModelBuilder.A(1).Description(null).Build()));
        }

        // ------------------------------------------------------------------ localised text

        [Fact]
        public void The_name_filter_matches_a_substring_case_insensitively()
        {
            var filter = new SpellModelTextFilter { Text = SpellText.Name, Query = "missile" };

            Assert.True(filter.Filter(SpellModelBuilder.A(1).Name("Arcane MISSILE").Build()));
            Assert.False(filter.Filter(SpellModelBuilder.A(2).Name("Healing Wave").Build()));
        }

        [Fact]
        public void The_tooltip_filter_reads_the_action_bar_tooltip()
        {
            var filter = new SpellModelTextFilter { Text = SpellText.Tooltip, Query = "over time" };

            Assert.True(filter.Filter(SpellModelBuilder.A(1).Tooltip("Damage over time").Build()));
            Assert.False(filter.Filter(SpellModelBuilder.A(2).Tooltip("Instant damage").Build()));
        }

        [Fact]
        public void Unresolved_localised_text_is_treated_as_no_text_at_all()
        {
            // GetText hands back a literal sentinel for an id it cannot resolve. Matching it would make
            // a search for "unknown" return every spell with a missing localisation.
            var filter = new SpellModelTextFilter { Text = SpellText.Name, Query = "unknown" };

            Assert.False(filter.Filter(SpellModelBuilder.A(1).Name(SpellModelTextFilter.Unknown).Build()));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void An_empty_text_query_keeps_everything(string query)
        {
            Assert.True(new SpellModelTextFilter { Query = query }.Filter(SpellModelBuilder.A(1).Build()));
        }

        [Fact]
        public void A_text_filter_tolerates_a_spell_with_nothing_to_read()
        {
            var name = new SpellModelTextFilter { Text = SpellText.Name, Query = "x" };

            Assert.False(name.Filter(SpellModelBuilder.A(1).NoBase().Build()));
            Assert.False(name.Filter(SpellModelBuilder.A(1).Name(null).Build()));
        }

        [Fact]
        public void The_text_search_matches_the_localised_name()
        {
            // The name a player actually sees is the one most worth searching.
            var filter = new SpellModelTextSearchFilter { Query = "missile" };

            Assert.True(filter.Filter(SpellModelBuilder.A(1, "").Name("Arcane Missile").Build()));
            Assert.False(filter.Filter(SpellModelBuilder.A(2, "").Name(SpellModelTextFilter.Unknown).Build()));
        }
}
}
