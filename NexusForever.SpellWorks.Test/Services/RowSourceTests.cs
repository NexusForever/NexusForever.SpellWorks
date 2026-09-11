using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Services;
using NexusForever.SpellWorks.Core.Static;
using NexusForever.SpellWorks.Services;
using NexusForever.SpellWorks.Services.Filtering;
using NexusForever.SpellWorks.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Test.Services
{
    /// <summary>
    /// Flattening the model graph into grid rows, once per filter apply. Each view projects a different
    /// shape, and the filter form narrows it.
    /// </summary>
    public class RowSourceTests
    {
        private readonly FakeTableCatalog _catalog = new();
        private readonly FakeSpellModelService _models = new();

        private RowSource Rows() =>
            new(_models, new PassThroughFilterService(), _catalog, new FilterSchemaRegistry(_models, _catalog, new Preferences()));

        private ISpellModel AddSpell(uint id, string description = "", uint tier = 1)
        {
            var spell = new TestSpellModel
            {
                Entry = new Spell4Entry { Id = id, TierIndex = tier },
                Description = description,
                SpellBaseModel = new TestSpellBaseModel { Entry = new Spell4BaseEntry() }
            };

            _models.SpellModels[id] = spell;
            return spell;
        }

        // ------------------------------------------------------------------ Spell4

        [Fact]
        public void The_spell_browser_projects_one_row_per_spell()
        {
            AddSpell(1, "Arcane Missile");
            AddSpell(2, "Healing Wave");

            GridData data = Rows().Build(PaneDescriptor.Spell4, Q.All(), null);

            Assert.Equal(2, data.Rows.Length);
            Assert.Equal(2, data.Total);
        }

        [Fact]
        public void A_spell_row_carries_the_columns_the_header_promises()
        {
            var spell = (TestSpellModel)AddSpell(7, "Arcane Missile", tier: 3);
            spell.SpellBaseModel.Entry.ClassIdPlayer = (byte)Class.Esper;
            spell.SpellBaseModel.Entry.School = (uint)DamageType.Magic;
            spell.SpellBaseModel.Entry.CastMethod = (byte)CastMethod.Channeled;
            spell.Effects.Add(new TestEffectModel { Entry = new Spell4EffectsEntry() });
            spell.Procs.Add(new TestProcModel { SpellId = 99 });

            GridData data = Rows().Build(PaneDescriptor.Spell4, Q.All(), null);
            GridRow row = data.Rows.Single();

            Assert.Equal(7u, row.Key);
            Assert.Equal(["7", "Arcane Missile", "3", nameof(Class.Esper), nameof(DamageType.Magic),
                          nameof(CastMethod.Channeled), "1", "1"], row.Cells);
            Assert.Same(spell, row.Source);
        }

        [Fact]
        public void A_spell_with_no_description_still_projects_a_cell()
        {
            ((TestSpellModel)AddSpell(1)).Description = null;

            Assert.Equal("", Rows().Build(PaneDescriptor.Spell4, Q.All(), null).Rows[0].Cells[1]);
        }

        [Fact]
        public void The_browser_reports_the_unfiltered_total_alongside_the_filtered_rows()
        {
            AddSpell(1, "Arcane Missile");
            AddSpell(2, "Healing Wave");

            GridData data = Rows().Build(PaneDescriptor.Spell4, Q.Searching("missile"), null);

            Assert.Single(data.Rows);
            Assert.Equal(2, data.Total);
        }

        [Fact]
        public void The_browser_columns_are_stable_and_sized()
        {
            GridData data = Rows().Build(PaneDescriptor.Spell4, Q.All(), null);

            Assert.Equal(8, data.Columns.Count);
            Assert.Equal("Description", data.Columns[1].Name);
            Assert.Equal(560, data.Columns[1].Width);
            Assert.All(data.Columns, c => Assert.True(c.Width > 0));
        }

        // ------------------------------------------------------------------ effects

        [Fact]
        public void The_effects_view_lists_the_effects_of_the_pane_spell()
        {
            var spell = (TestSpellModel)AddSpell(1);
            spell.Effects.Add(new TestEffectModel { Entry = new Spell4EffectsEntry(), Type = SpellEffectType.Damage });
            spell.Effects.Add(new TestEffectModel { Entry = new Spell4EffectsEntry(), Type = SpellEffectType.Heal });

            GridData data = Rows().Build(PaneDescriptor.Effects, Q.All(), spell);

            Assert.Equal(2, data.Rows.Length);
            Assert.Equal(11, data.Columns.Count);
        }

        [Fact]
        public void The_effects_view_is_empty_until_a_spell_is_selected()
        {
            GridData data = Rows().Build(PaneDescriptor.Effects, Q.All(), null);

            Assert.Empty(data.Rows);
            Assert.Equal(0, data.Total);
            Assert.NotEmpty(data.Columns);
        }

        [Fact]
        public void An_effect_row_falls_back_to_the_raw_data_bits_when_there_is_no_projection()
        {
            var spell = (TestSpellModel)AddSpell(1);
            spell.Effects.Add(new TestEffectModel
            {
                Entry = new Spell4EffectsEntry { DataBits00 = 11, DataBits01 = 22, DataBits02 = 33 },
                Type = SpellEffectType.Damage,
                DelayTime = 250,
                TickTime = 100,
                DurationTime = 5000,
                Flags = 6
            });

            GridRow row = Rows().Build(PaneDescriptor.Effects, Q.All(), spell).Rows.Single();

            Assert.Equal("0", row.Cells[0]);
            Assert.Equal(nameof(SpellEffectType.Damage), row.Cells[2]);
            Assert.Equal("250", row.Cells[4]);
            Assert.Equal("100", row.Cells[5]);
            Assert.Equal("5000", row.Cells[6]);
            Assert.Equal("6", row.Cells[7]);
            Assert.Equal(["11", "22", "33"], row.Cells[8..11]);
        }

        [Theory]
        [InlineData("Heal", 1)]
        [InlineData("Any", 2)]
        public void Effects_can_be_narrowed_to_one_type(string type, int expected)
        {
            var spell = (TestSpellModel)AddSpell(1);
            spell.Effects.Add(new TestEffectModel { Entry = new Spell4EffectsEntry(), Type = SpellEffectType.Damage });
            spell.Effects.Add(new TestEffectModel { Entry = new Spell4EffectsEntry(), Type = SpellEffectType.Heal });

            GridData data = Rows().Build(PaneDescriptor.Effects, Q.With(FilterFields.EffectType, type), spell);

            Assert.Equal(expected, data.Rows.Length);
            Assert.Equal(2, data.Total);
        }

        [Fact]
        public void A_narrowed_effect_row_keeps_its_own_Spell4Effects_index()
        {
            // The "#" cell and the row key are the effect's position in the spell's own row list, which is
            // what the context menu prints as "idx" and what the Detail cards number themselves by. Counting
            // the filtered sequence instead renumbers the survivors from zero, so a narrowed grid names the
            // wrong effect.
            var spell = (TestSpellModel)AddSpell(1);
            spell.Effects.Add(new TestEffectModel { Entry = new Spell4EffectsEntry(), Type = SpellEffectType.Damage });
            spell.Effects.Add(new TestEffectModel { Entry = new Spell4EffectsEntry(), Type = SpellEffectType.Heal });

            GridRow row = Rows()
                .Build(PaneDescriptor.Effects, Q.With(FilterFields.EffectType, nameof(SpellEffectType.Heal)), spell)
                .Rows.Single();

            Assert.Equal("1", row.Cells[0]);
            Assert.Equal(1u, row.Key);
        }

        [Fact]
        public void Effects_can_be_narrowed_by_flags_tick_and_duration()
        {
            var spell = (TestSpellModel)AddSpell(1);
            spell.Effects.Add(new TestEffectModel { Entry = new Spell4EffectsEntry(), Flags = 0b0110, TickTime = 100, DurationTime = 5000 });
            spell.Effects.Add(new TestEffectModel { Entry = new Spell4EffectsEntry(), Flags = 0b0001, TickTime = 10, DurationTime = 100 });

            RowSource rows = Rows();

            Assert.Single(rows.Build(PaneDescriptor.Effects, Q.With(FilterFields.EffectFlags, "0x06", FilterOperator.MaskAll), spell).Rows);
            Assert.Single(rows.Build(PaneDescriptor.Effects, Q.With(FilterFields.EffectTick, "50", FilterOperator.AtLeast), spell).Rows);
            Assert.Single(rows.Build(PaneDescriptor.Effects, Q.With(FilterFields.EffectDuration, "1000", FilterOperator.AtLeast), spell).Rows);
        }

        [Fact]
        public void Searching_effects_matches_the_effect_type_name()
        {
            var spell = (TestSpellModel)AddSpell(1);
            spell.Effects.Add(new TestEffectModel { Entry = new Spell4EffectsEntry(), Type = SpellEffectType.Damage });
            spell.Effects.Add(new TestEffectModel { Entry = new Spell4EffectsEntry(), Type = SpellEffectType.Heal });

            Assert.Single(Rows().Build(PaneDescriptor.Effects, Q.Searching("heal"), spell).Rows);
        }

        // ------------------------------------------------------------------ procs

        [Fact]
        public void The_procs_view_resolves_the_description_of_the_spell_a_proc_casts()
        {
            AddSpell(99, "Arcane Missile");
            var spell = (TestSpellModel)AddSpell(1);
            spell.Procs.Add(new TestProcModel { SpellId = 99, ProcType = (ProcType)3 });

            GridRow row = Rows().Build(PaneDescriptor.Procs, Q.All(), spell).Rows.Single();

            Assert.Equal(["3", "99", "Arcane Missile"], row.Cells);
            Assert.Equal(99u, row.Key);
        }

        [Fact]
        public void A_proc_pointing_at_a_spell_that_is_not_loaded_says_so()
        {
            var spell = (TestSpellModel)AddSpell(1);
            spell.Procs.Add(new TestProcModel { SpellId = 12345 });

            Assert.Equal("unknown spell", Rows().Build(PaneDescriptor.Procs, Q.All(), spell).Rows[0].Cells[2]);
        }

        [Fact]
        public void The_procs_view_is_empty_until_a_spell_is_selected()
        {
            Assert.Empty(Rows().Build(PaneDescriptor.Procs, Q.All(), null).Rows);
        }

        [Fact]
        public void Procs_can_be_narrowed_by_type_and_by_target_spell()
        {
            var spell = (TestSpellModel)AddSpell(1);
            spell.Procs.Add(new TestProcModel { SpellId = 100, ProcType = (ProcType)1 });
            spell.Procs.Add(new TestProcModel { SpellId = 200, ProcType = (ProcType)2 });

            RowSource rows = Rows();

            Assert.Single(rows.Build(PaneDescriptor.Procs, Q.With(FilterFields.ProcType, "1"), spell).Rows);
            Assert.Single(rows.Build(PaneDescriptor.Procs, Q.With(FilterFields.ProcSpellId, "2", FilterOperator.StartsWith), spell).Rows);
        }

        [Fact]
        public void Procs_can_be_narrowed_to_those_referenced_elsewhere()
        {
            var spell = (TestSpellModel)AddSpell(1);
            spell.Procs.Add(new TestProcModel { SpellId = 100 });
            spell.Procs.Add(new TestProcModel { SpellId = 200 });
            _models.SpellProcReferences[100] = [1];

            GridData data = Rows().Build(PaneDescriptor.Procs, Q.With(FilterFields.ProcReferenced, "", FilterOperator.IsSet), spell);

            Assert.Equal("100", Assert.Single(data.Rows).Cells[1]);
        }

        [Fact]
        public void Searching_procs_asks_the_two_boxes_separately()
        {
            AddSpell(100, "Arcane Missile");
            var spell = (TestSpellModel)AddSpell(1);
            spell.Procs.Add(new TestProcModel { SpellId = 100 });
            spell.Procs.Add(new TestProcModel { SpellId = 200 });

            RowSource rows = Rows();

            Assert.Single(rows.Build(PaneDescriptor.Procs, Q.Searching("missile"), spell).Rows);
            Assert.Single(rows.Build(PaneDescriptor.Procs, Q.SearchingId("200"), spell).Rows);

            // The text box does not match the id; that is the id box's job.
            Assert.Empty(rows.Build(PaneDescriptor.Procs, Q.Searching("200"), spell).Rows);
        }

        [Fact]
        public void The_two_search_boxes_are_anded()
        {
            AddSpell(100, "Arcane Missile");
            AddSpell(200, "Arcane Shield");
            var spell = (TestSpellModel)AddSpell(1);
            spell.Procs.Add(new TestProcModel { SpellId = 100 });
            spell.Procs.Add(new TestProcModel { SpellId = 200 });

            GridData data = Rows().Build(PaneDescriptor.Procs,
                Q.Searching("arcane").SearchingId("100"), spell);

            Assert.Single(data.Rows);
            Assert.Equal(100u, data.Rows[0].Key);
        }

        [Fact]
        public void An_exact_search_matches_the_whole_value()
        {
            AddSpell(1, "Arcane Missile");
            AddSpell(2, "Arcane Missile II");

            RowSource rows = Rows();

            Assert.Equal(2, rows.Build(PaneDescriptor.Spell4, Q.Searching("Arcane Missile"), null).Rows.Length);
            Assert.Single(rows.Build(PaneDescriptor.Spell4, Q.Searching("Arcane Missile").Exactly(), null).Rows);
        }

        [Fact]
        public void An_exact_id_search_does_not_match_a_longer_id()
        {
            AddSpell(715, "");
            AddSpell(7157, "");

            RowSource rows = Rows();

            Assert.Equal(2, rows.Build(PaneDescriptor.Spell4, Q.SearchingId("715"), null).Rows.Length);
            Assert.Single(rows.Build(PaneDescriptor.Spell4, Q.SearchingId("715").Exactly(), null).Rows);
        }

        // ------------------------------------------------------------------ table list

        [Fact]
        public void The_table_list_projects_one_row_per_catalog_entry()
        {
            _catalog.With("Spell4", ["Id", "Description"], [["1", "x"]]);
            _catalog.With("Spell4Base", ["Id"], []);

            GridData data = Rows().Build(PaneDescriptor.Tables, Q.All(), null);

            Assert.Equal(2, data.Rows.Length);
            Assert.Equal(["Spell4", "1", "2", "loaded"], data.Rows[0].Cells);
            Assert.Equal(["Spell4Base", "0", "1", "empty"], data.Rows[1].Cells);
        }

        [Fact]
        public void The_table_list_can_be_narrowed_by_name_and_to_loaded_tables()
        {
            _catalog.With("Spell4", ["Id"], [["1"]]);
            _catalog.With("Spell4Base", ["Id"], []);
            _catalog.With("Creature2", ["Id"], [["1"]]);

            RowSource rows = Rows();

            Assert.Equal(2, rows.Build(PaneDescriptor.Tables, Q.With(FilterFields.TableName, "spell", FilterOperator.StartsWith), null).Rows.Length);
            Assert.Equal(2, rows.Build(PaneDescriptor.Tables, Q.With(FilterFields.TableLoaded, "", FilterOperator.IsSet), null).Rows.Length);
            Assert.Single(rows.Build(PaneDescriptor.Tables, Q.Searching("creature"), null).Rows);
        }

        // ------------------------------------------------------------------ generic game table

        private PaneDescriptor Table(string name) =>
            new(PaneDescriptor.GameTableId(name), PaneKind.GameTable, name + ".tbl", name, "icon", "meta", name);

        [Fact]
        public void A_game_table_projects_its_own_columns_with_the_id_column_first()
        {
            _catalog.With("Spell4", ["Id", "Description", "Flags"], [["1", "Arcane", "6"]]);

            GridData data = Rows().Build(Table("Spell4"), Q.All(), null);

            Assert.Equal(3, data.Columns.Count);
            Assert.Equal(96, data.Columns[0].Width);
            Assert.Equal("id", data.Columns[0].CellClass);
            Assert.Equal(128, data.Columns[1].Width);
            Assert.Equal("num", data.Columns[1].CellClass);
            Assert.Equal(["1", "Arcane", "6"], data.Rows.Single().Cells);
        }

        [Fact]
        public void A_game_table_row_is_keyed_by_its_id_column()
        {
            _catalog.With("Spell4", ["Id"], [["42"]]);

            Assert.Equal(42u, Rows().Build(Table("Spell4"), Q.All(), null).Rows[0].Key);
        }

        [Fact]
        public void A_row_whose_id_is_not_a_number_falls_back_to_its_position()
        {
            _catalog.With("Spell4", ["Id"], [["not-a-number"]]);

            Assert.Equal(0u, Rows().Build(Table("Spell4"), Q.All(), null).Rows[0].Key);
        }

        [Fact]
        public void A_table_that_is_not_in_the_catalog_yields_nothing()
        {
            Assert.Equal(GridData.Empty, Rows().Build(Table("Missing"), Q.All(), null));
        }

        [Fact]
        public void A_game_table_can_be_narrowed_by_id_prefix_and_by_content()
        {
            _catalog.With("Spell4", ["Id", "Description"], [["12", "Arcane"], ["34", "Healing"]]);

            RowSource rows = Rows();

            Assert.Single(rows.Build(Table("Spell4"), Q.With(FilterFields.RowId, "1", FilterOperator.StartsWith), null).Rows);
            Assert.Single(rows.Build(Table("Spell4"), Q.With(FilterFields.RowContains, "healing", FilterOperator.Contains), null).Rows);
            Assert.Single(rows.Build(Table("Spell4"), Q.Searching("arcane"), null).Rows);
        }

        [Fact]
        public void A_game_table_can_be_narrowed_by_a_flags_column_when_it_has_one()
        {
            _catalog.With("Spell4", ["Id", "Flags"], [["1", "6"], ["2", "1"]]);

            GridData data = Rows().Build(Table("Spell4"), Q.With(FilterFields.RowFlags, "0x06", FilterOperator.MaskAll), null);

            Assert.Equal("1", Assert.Single(data.Rows).Cells[0]);
        }

        [Fact]
        public void A_flags_filter_on_a_table_with_no_flags_column_is_ignored()
        {
            _catalog.With("Spell4", ["Id", "Description"], [["1", "x"], ["2", "y"]]);

            Assert.Equal(2, Rows().Build(Table("Spell4"), Q.With(FilterFields.RowFlags, "6", FilterOperator.MaskAll), null).Rows.Length);
        }

        [Fact]
        public void Empty_rows_can_be_hidden()
        {
            // A row whose every column past the id is zero or blank carries no information.
            _catalog.With("Spell4", ["Id", "A", "B"], [["1", "0", ""], ["2", "5", "0"], ["3", "0", "0.0"]]);

            GridData data = Rows().Build(Table("Spell4"), Q.With(FilterFields.RowNonZero, "", FilterOperator.IsSet), null);

            Assert.Equal("2", Assert.Single(data.Rows).Cells[0]);
            Assert.Equal(3, data.Total);
        }

        [Fact]
        public void The_detail_and_setup_views_have_no_grid_of_their_own()
        {
            RowSource rows = Rows();

            Assert.Equal(GridData.Empty, rows.Build(PaneDescriptor.Detail, Q.All(), null));
            Assert.Equal(GridData.Empty, rows.Build(PaneDescriptor.Setup, Q.All(), null));
        }

        // ------------------------------------------------------------------ effect types

        private EffectTypeUsage AddUsage(SpellEffectType type, uint[] spellIds, int rows = 0)
        {
            var usage = new EffectTypeUsage
            {
                Type           = type,
                SpellIds       = spellIds,
                EffectRowCount = rows == 0 ? spellIds.Length : rows
            };

            _models.EffectTypeUsages[type] = usage;
            return usage;
        }

        [Fact]
        public void The_effect_type_browser_projects_one_row_per_used_type()
        {
            AddUsage(SpellEffectType.Damage, [1, 2]);
            AddUsage(SpellEffectType.Heal, [3]);

            GridData data = Rows().Build(PaneDescriptor.EffectTypes, Q.All(), null);

            Assert.Equal(2, data.Rows.Length);
            Assert.Equal(2, data.Total);
        }

        [Fact]
        public void An_effect_type_row_carries_the_columns_the_header_promises()
        {
            EffectTypeUsage usage = AddUsage(SpellEffectType.Damage, [1, 2], rows: 5);

            GridData data = Rows().Build(PaneDescriptor.EffectTypes, Q.All(), null);
            GridRow row = data.Rows.Single();

            Assert.Equal((uint)SpellEffectType.Damage, row.Key);
            Assert.Equal([((uint)SpellEffectType.Damage).ToString(), nameof(SpellEffectType.Damage), "2", "5"], row.Cells);
            Assert.Same(usage, row.Source);
        }

        [Fact]
        public void The_effect_type_browser_is_ordered_by_type_id()
        {
            AddUsage(SpellEffectType.Heal, [1]);
            AddUsage(SpellEffectType.Damage, [1]);

            GridData data = Rows().Build(PaneDescriptor.EffectTypes, Q.All(), null);

            Assert.Equal(data.Rows.Select(r => r.Key).Order(), data.Rows.Select(r => r.Key));
        }

        [Fact]
        public void The_effect_type_browser_can_be_narrowed_by_id_prefix_minimum_and_search()
        {
            AddUsage(SpellEffectType.Damage, [1, 2, 3]);
            AddUsage(SpellEffectType.Heal, [4]);

            RowSource rows = Rows();
            string damageId = ((uint)SpellEffectType.Damage).ToString();

            Assert.Single(rows.Build(PaneDescriptor.EffectTypes, Q.With(FilterFields.TypeId, damageId, FilterOperator.StartsWith), null).Rows);
            Assert.Single(rows.Build(PaneDescriptor.EffectTypes, Q.With(FilterFields.TypeSpells, "2", FilterOperator.AtLeast), null).Rows);
            Assert.Single(rows.Build(PaneDescriptor.EffectTypes, Q.Searching("heal"), null).Rows);
            Assert.Single(rows.Build(PaneDescriptor.EffectTypes, Q.SearchingId(damageId), null).Rows);
        }

        [Fact]
        public void A_min_spells_filter_that_is_not_a_number_is_ignored()
        {
            AddUsage(SpellEffectType.Damage, [1]);

            Assert.Single(Rows().Build(PaneDescriptor.EffectTypes, Q.With(FilterFields.TypeSpells, "lots", FilterOperator.AtLeast), null).Rows);
        }

        // ------------------------------------------------------------------ effect type spells

        [Fact]
        public void The_spells_behind_an_effect_type_carry_their_id_and_their_description()
        {
            // The description is the point of the view: an id alone says nothing about what the spell does.
            AddSpell(11, "Arcane Missile", tier: 3);
            AddUsage(SpellEffectType.Damage, [11]);

            GridData data = Rows().Build(PaneDescriptor.EffectTypeSpells, Q.All(), null, SpellEffectType.Damage);
            GridRow row = data.Rows.Single();

            Assert.Equal(11u, row.Key);
            Assert.Equal("11", row.Cells[0]);
            Assert.Equal("Arcane Missile", row.Cells[1]);
            Assert.Equal("3", row.Cells[2]);
            Assert.Same(_models.SpellModels[11], row.Source);
        }

        [Fact]
        public void A_spell_is_counted_by_how_many_effects_of_the_listed_type_it_carries()
        {
            var spell = (TestSpellModel)AddSpell(11, "Arcane Missile");
            spell.Effects.Add(new TestEffectModel { Type = SpellEffectType.Damage, Entry = new Spell4EffectsEntry() });
            spell.Effects.Add(new TestEffectModel { Type = SpellEffectType.Damage, Entry = new Spell4EffectsEntry() });
            spell.Effects.Add(new TestEffectModel { Type = SpellEffectType.Heal, Entry = new Spell4EffectsEntry() });

            AddUsage(SpellEffectType.Damage, [11]);

            GridData data = Rows().Build(PaneDescriptor.EffectTypeSpells, Q.All(), null, SpellEffectType.Damage);

            Assert.Equal("2", data.Rows.Single().Cells[5]);
        }

        [Fact]
        public void The_spells_behind_an_effect_type_keep_the_index_order()
        {
            AddSpell(11, "One");
            AddSpell(22, "Two");
            AddUsage(SpellEffectType.Damage, [11, 22]);

            GridData data = Rows().Build(PaneDescriptor.EffectTypeSpells, Q.All(), null, SpellEffectType.Damage);

            Assert.Equal([11u, 22u], data.Rows.Select(r => r.Key));
        }

        [Fact]
        public void A_spell_the_index_names_but_the_engine_never_loaded_is_skipped()
        {
            AddSpell(11, "Arcane Missile");
            AddUsage(SpellEffectType.Damage, [11, 4242]);

            GridData data = Rows().Build(PaneDescriptor.EffectTypeSpells, Q.All(), null, SpellEffectType.Damage);

            Assert.Equal(11u, Assert.Single(data.Rows).Key);
            Assert.Equal(1, data.Total);
        }

        [Fact]
        public void The_spells_behind_an_effect_type_take_the_whole_spell4_filter_form()
        {
            AddSpell(11, "Arcane Missile");
            AddSpell(22, "Healing Wave");
            AddUsage(SpellEffectType.Damage, [11, 22]);

            GridData data = Rows().Build(PaneDescriptor.EffectTypeSpells,
                Q.Searching("healing"), null, SpellEffectType.Damage);

            Assert.Equal(22u, Assert.Single(data.Rows).Key);

            // Total stays the unfiltered size, so the header can say "1 of 2".
            Assert.Equal(2, data.Total);
        }

        [Fact]
        public void An_effect_type_spells_pane_with_nothing_picked_yet_is_empty_rather_than_broken()
        {
            AddSpell(11, "Arcane Missile");
            AddUsage(SpellEffectType.Damage, [11]);

            RowSource rows = Rows();

            // Null is the pane's start state; an unused type reaches the same place through the index.
            GridData none = rows.Build(PaneDescriptor.EffectTypeSpells, Q.All(), null, null);
            GridData unused = rows.Build(PaneDescriptor.EffectTypeSpells, Q.All(), null, SpellEffectType.Heal);

            Assert.Empty(none.Rows);
            Assert.Equal(0, none.Total);
            Assert.Empty(unused.Rows);

            // Still the real column set, so the grid renders its header rather than collapsing.
            Assert.Equal(6, none.Columns.Count);
            Assert.Equal("Description", none.Columns[1].Name);
        }

        // ------------------------------------------------------------------ boolean queries

        [Fact]
        public void Blocks_are_ored_across_the_projection()
        {
            AddSpell(1, "Arcane Missile");
            AddSpell(2, "Healing Wave");
            AddSpell(3, "Shield Wall");

            GridData data = Rows().Build(PaneDescriptor.Spell4,
                Q.With(FilterFields.Id, "1", FilterOperator.StartsWith)
                 .Or(FilterFields.Id, "2", FilterOperator.StartsWith), null);

            Assert.Equal([1u, 2u], data.Rows.Select(r => r.Key));
            Assert.Equal(3, data.Total);
        }

        [Fact]
        public void A_negated_condition_excludes_rather_than_selects()
        {
            AddSpell(1, "[DEPRECATED] Arcane Missile");
            AddSpell(2, "Healing Wave");

            GridData data = Rows().Build(PaneDescriptor.Spell4,
                Q.Toggling(FilterFields.Deprecated, negate: true), null);

            Assert.Equal([2u], data.Rows.Select(r => r.Key));
        }

        [Fact]
        public void A_block_whose_conditions_are_all_invalid_does_not_widen_the_result()
        {
            // The rule the whole boolean design turns on. A broken term inside an OR must narrow to nothing,
            // never open the grid back up to every row.
            AddSpell(1, "Arcane Missile");
            AddSpell(2, "Healing Wave");
            AddSpell(3, "Shield Wall");

            GridData data = Rows().Build(PaneDescriptor.Spell4,
                Q.With(FilterFields.Id, "1", FilterOperator.StartsWith)
                 .Or(FilterFields.School, "NotASchool"), null);

            Assert.Equal([1u], data.Rows.Select(r => r.Key));
        }

        [Fact]
        public void The_common_band_narrows_every_block()
        {
            AddSpell(11, "Arcane Missile");
            AddSpell(12, "Healing Wave");
            AddSpell(21, "Shield Wall");

            FilterQuery query = Q.With(FilterFields.Id, "11", FilterOperator.StartsWith)
                                 .Or(FilterFields.Id, "12", FilterOperator.StartsWith);
            query.MakeCommon(query.Set(FilterFields.Id, "1", FilterOperator.StartsWith));

            // Set replaced the two block conditions, so this is the band alone - the point being that a
            // common condition applies without belonging to any one block.
            GridData data = Rows().Build(PaneDescriptor.Spell4, query, null);

            Assert.Equal([11u, 12u], data.Rows.Select(r => r.Key));
        }

        [Fact]
        public void The_total_stays_unfiltered_however_the_query_is_shaped()
        {
            AddSpell(1, "Arcane Missile");
            AddSpell(2, "Healing Wave");

            GridData data = Rows().Build(PaneDescriptor.Spell4,
                Q.With(FilterFields.Id, "1", FilterOperator.StartsWith)
                 .Or(FilterFields.Id, "9", FilterOperator.StartsWith), null);

            Assert.Single(data.Rows);
            Assert.Equal(2, data.Total);
        }

        // ------------------------------------------------------------------ doubles

        // ------------------------------------------------------------------ form shim

        private sealed class TestSpellModel : ISpellModel
        {
            public Spell4Entry Entry { get; set; }
            public uint Id => Entry.Id;
            public string Description { get; set; } = "";
            public string ActionBarTooltip => "";
            public ISpellBaseModel SpellBaseModel { get; set; }
            public List<ISpellEffectModel> Effects { get; } = [];
            public List<ISpellProcModel> Procs { get; } = [];
            public List<uint> ProcReferences { get; } = [];

            public void Initialise(Spell4Entry entry) => Entry = entry;
        }

        private sealed class TestSpellBaseModel : ISpellBaseModel
        {
            public Spell4BaseEntry Entry { get; set; }
            public string Name => "";
            public Spell4HitResultsEntry HitResult => null;
            public Spell4TargetMechanicsEntry TargetMechanics { get; set; } = new();
            public Spell4TargetAngleEntry TargetAngle => null;
            public Spell4PrerequisitesEntry Prerequisites => null;
            public Spell4ValidTargetsEntry ValidTargets => null;
            public TargetGroupEntry CastGroup => null;
            public Creature2Entry PositionalAoe => null;
            public TargetGroupEntry AoeGroup => null;
            public Spell4BaseEntry PrerequisiteSpell => null;
            public Spell4SpellTypesEntry SpellType => null;

            public void Initialise(Spell4BaseEntry entry) => Entry = entry;
        }

        private sealed class TestEffectModel : ISpellEffectModel
        {
            public Spell4EffectsEntry Entry { get; set; }
            public SpellEffectType Type { get; set; }
            public uint TargetFlags { get; set; }
            public uint DamageType { get; set; }
            public uint DelayTime { get; set; }
            public uint TickTime { get; set; }
            public uint DurationTime { get; set; }
            public uint Flags { get; set; }
            public ISpellEffectColumnData ColumnData => null;
            public List<ISpellEffectRowData> RowData { get; } = [];

            public void Initialise(Spell4EffectsEntry entry) => Entry = entry;
        }

        private sealed class TestProcModel : ISpellProcModel
        {
            public Spell4EffectsEntry Entry { get; set; }
            public ProcType ProcType { get; set; }
            public uint SpellId { get; set; }

            public void Initialise(Spell4EffectsEntry entry) => Entry = entry;
        }
    }
}
