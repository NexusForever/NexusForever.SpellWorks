using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;
using NexusForever.SpellWorks.Components;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Services;
using NexusForever.SpellWorks.Core.Static;
using NexusForever.SpellWorks.Services;
using NexusForever.SpellWorks.Services.Filtering;
using NexusForever.SpellWorks.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Test.Components
{
    /// <summary>
    /// The shared behaviour panes reach for: opening a view, and the context menus they raise. Each menu
    /// is driven by invoking the item a user would click.
    /// </summary>
    public class ShellContextTests : ComponentTestContext
    {
        private ShellContext Ctx => _ctx ??= Context();
        private ShellContext _ctx;

        private static void Run(List<MenuItem> items, string label) =>
            items.Single(i => i.Label == label).Invoke();

        // ------------------------------------------------------------------ menu lifetime

        [Fact]
        public void No_menu_is_open_to_begin_with()
        {
            Assert.Null(Ctx.Menu);
        }

        [Fact]
        public void Showing_a_menu_records_where_it_was_raised()
        {
            Ctx.ShowMenu(120, 40, "Title", "sub", [new MenuItem("icon", "Item", "", () => { })]);

            Assert.Equal(120, Ctx.Menu.X);
            Assert.Equal(40, Ctx.Menu.Y);
            Assert.Equal("Title", Ctx.Menu.Title);
            Assert.Equal("sub", Ctx.Menu.Sub);
        }

        [Fact]
        public void Closing_a_menu_that_is_not_open_is_harmless()
        {
            Ctx.CloseMenu();

            Assert.Null(Ctx.Menu);
        }

        [Fact]
        public void Running_a_menu_item_dismisses_the_menu_first()
        {
            var ran = false;
            List<MenuItem> items = Ctx.TableMenu(Table("Spell4"));
            Ctx.ShowMenu(0, 0, "t", "s", items);

            items[0].Invoke();
            ran = true;

            Assert.True(ran);
            Assert.Null(Ctx.Menu);
        }

        // ------------------------------------------------------------------ navigation

        [Fact]
        public void Opening_a_view_activates_it_here_or_pops_it_out()
        {
            Ctx.Open(PaneDescriptor.Tables.Id, popout: false);
            Assert.Equal(PaneDescriptor.Tables.Id, State.Active);

            Ctx.Open(PaneDescriptor.Effects.Id, popout: true);
            Assert.Equal([PaneDescriptor.Effects.Id], Windows.Popped);
        }

        [Fact]
        public void Opening_a_spell_selects_it_and_shows_the_requested_section()
        {
            Ctx.OpenDetail(42, DetailSubTab.Procs, popout: false);

            Assert.Equal(42u, State.SelectedSpellId);
            Assert.Equal(PaneDescriptor.Detail.Id, State.Active);
            Assert.Equal(DetailSubTab.Procs, State.PaneStateFor(PaneDescriptor.Detail.Id).SubTab);
        }

        [Fact]
        public void Opening_a_spell_in_a_new_window_still_selects_it()
        {
            Ctx.OpenDetail(42, DetailSubTab.Spell, popout: true);

            Assert.Equal(42u, State.SelectedSpellId);
            Assert.Equal([PaneDescriptor.Detail.Id], Windows.Popped);
        }

        [Fact]
        public async Task Copying_puts_the_text_on_the_clipboard()
        {
            await Ctx.Copy("12345");

            Assert.Equal("12345", InvocationArgs("copyText")[0]);
        }

        [Fact]
        public async Task Copying_nothing_still_writes_an_empty_clipboard_rather_than_failing()
        {
            await Ctx.Copy(null);

            Assert.Equal("", InvocationArgs("copyText")[0]);
        }

        [Fact]
        public async Task Copying_before_the_module_is_loaded_is_a_no_op()
        {
            var context = new ShellContext { State = State, Popouts = Popouts, Refresh = () => Task.CompletedTask };

            await context.Copy("12345");

            Assert.Equal(0, InvocationCount("copyText"));
        }

        // ------------------------------------------------------------------ spell menu

        [Fact]
        public void The_spell_menu_offers_to_open_filter_and_copy()
        {
            ISpellModel spell = Spell(7, "Arcane Missile");

            List<MenuItem> menu = Ctx.SpellMenu(spell, PaneDescriptor.Spell4.Id);

            Assert.Equal(6, menu.Count);
            Assert.Contains(menu, i => i.Label == "Open in Detail");
            Assert.Contains(menu, i => i.Label == "Show 0 effects");
            Assert.Contains(menu, i => i.Label == "Show 0 procs");
            Assert.Contains(menu, i => i.Label == "Copy spell id" && i.Hint == "7");
        }

        [Fact]
        public void The_spell_menu_opens_the_spell_in_the_detail_view()
        {
            List<MenuItem> menu = Ctx.SpellMenu(Spell(7), PaneDescriptor.Spell4.Id);

            Run(menu, "Open in Detail");

            Assert.Equal(7u, State.SelectedSpellId);
            Assert.Equal(PaneDescriptor.Detail.Id, State.Active);
        }

        [Fact]
        public void The_spell_menu_can_open_the_spell_in_a_new_window()
        {
            Run(Ctx.SpellMenu(Spell(7), PaneDescriptor.Spell4.Id), "Open in new window");

            Assert.Equal([PaneDescriptor.Detail.Id], Windows.Popped);
        }

        [Fact]
        public void The_spell_menu_jumps_straight_to_the_effects_or_procs_section()
        {
            List<MenuItem> menu = Ctx.SpellMenu(Spell(7), PaneDescriptor.Spell4.Id);

            Run(menu, "Show 0 effects");
            Assert.Equal(DetailSubTab.Effects, State.PaneStateFor(PaneDescriptor.Detail.Id).SubTab);

            Run(menu, "Show 0 procs");
            Assert.Equal(DetailSubTab.Procs, State.PaneStateFor(PaneDescriptor.Detail.Id).SubTab);
        }

        [Fact]
        public void The_spell_menu_filters_the_browser_to_the_spells_class()
        {
            ISpellModel spell = Spell(7);
            ((TestBase)spell.SpellBaseModel).Entry.ClassIdPlayer = (byte)Class.Esper;

            Run(Ctx.SpellMenu(spell, PaneDescriptor.Spell4.Id), $"Filter to {nameof(Class.Esper)}");

            PaneState pane = State.PaneStateFor(PaneDescriptor.Spell4.Id);
            Assert.Equal(nameof(Class.Esper), pane.Filters.ValueOf(FilterFields.Class));
            Assert.Equal(PaneMode.Rows, pane.Mode);
        }

        [Fact]
        public async Task The_spell_menu_copies_the_spell_id()
        {
            Run(Ctx.SpellMenu(Spell(7), PaneDescriptor.Spell4.Id), "Copy spell id");

            await Task.Yield();
            Assert.Equal("7", InvocationArgs("copyText")[0]);
        }

        // ------------------------------------------------------------------ effect menu

        [Fact]
        public void The_effect_menu_opens_the_effects_section_and_filters_by_type()
        {
            ISpellEffectModel effect = Effect(SpellEffectType.Damage);

            List<MenuItem> menu = Ctx.EffectMenu(effect, 7, PaneDescriptor.Effects.Id);
            Assert.Equal(4, menu.Count);

            Run(menu, "Open in Detail");
            Assert.Equal(DetailSubTab.Effects, State.PaneStateFor(PaneDescriptor.Detail.Id).SubTab);

            Run(menu, $"Filter to {nameof(SpellEffectType.Damage)}");
            Assert.Equal(nameof(SpellEffectType.Damage),
                State.PaneStateFor(PaneDescriptor.Effects.Id).Filters.ValueOf(FilterFields.EffectType));
        }

        [Fact]
        public void The_effect_menu_can_open_the_spell_in_a_new_window()
        {
            Run(Ctx.EffectMenu(Effect(SpellEffectType.Damage), 7, PaneDescriptor.Effects.Id), "Open in new window");

            Assert.Equal([PaneDescriptor.Detail.Id], Windows.Popped);
        }

        [Fact]
        public async Task The_effect_menu_copies_the_whole_data_row_tab_separated()
        {
            ISpellEffectModel effect = Effect(SpellEffectType.Damage);
            effect.RowData.Add(new NexusForever.SpellWorks.Core.Models.Effect.DefaultSpellEffectRowData
            {
                Entry = new Spell4EffectsEntry { DataBits00 = 1, DataBits01 = 2 }
            });

            Run(Ctx.EffectMenu(effect, 7, PaneDescriptor.Effects.Id), "Copy data row");

            await Task.Yield();
            Assert.StartsWith("1\t2\t", (string)InvocationArgs("copyText")[0]);
        }

        [Fact]
        public async Task An_effect_with_no_projection_copies_nothing_rather_than_failing()
        {
            Run(Ctx.EffectMenu(Effect(SpellEffectType.Damage), 7, PaneDescriptor.Effects.Id), "Copy data row");

            await Task.Yield();
            Assert.Equal("", InvocationArgs("copyText")[0]);
        }

        // ------------------------------------------------------------------ effect type menu

        private EffectTypeUsage Usage(SpellEffectType type, uint[] spellIds, int rows = 1)
        {
            var usage = new EffectTypeUsage { Type = type, SpellIds = spellIds, EffectRowCount = rows };
            Models.EffectTypeUsages[type] = usage;
            return usage;
        }

        [Fact]
        public void The_effect_type_menu_shows_the_spells_behind_the_type()
        {
            EffectTypeUsage usage = Usage(SpellEffectType.Damage, [1, 2, 3]);

            List<MenuItem> menu = Ctx.EffectTypeMenu(usage);
            Assert.Equal(4, menu.Count);

            // The count is on the label, so the menu says how much is behind the click.
            Run(menu, "Show 3 spells");

            Assert.Equal(SpellEffectType.Damage, State.SelectedEffectType);
            Assert.Equal(PaneDescriptor.EffectTypeSpells.Id, State.Active);
        }

        [Fact]
        public void The_effect_type_menu_can_show_the_spells_in_a_new_window()
        {
            Run(Ctx.EffectTypeMenu(Usage(SpellEffectType.Damage, [1])), "Show in new window");

            Assert.Equal([PaneDescriptor.EffectTypeSpells.Id], Windows.Popped);
            Assert.Equal(SpellEffectType.Damage, State.SelectedEffectType);
        }

        [Fact]
        public void The_effect_type_menu_narrows_the_spell_browser_to_the_type()
        {
            Run(Ctx.EffectTypeMenu(Usage(SpellEffectType.Damage, [1])), $"Filter Spell4 to {nameof(SpellEffectType.Damage)}");

            PaneState spells = State.PaneStateFor(PaneDescriptor.Spell4.Id);

            Assert.Equal(nameof(SpellEffectType.Damage), spells.Filters.ValueOf(FilterFields.EffectType));
            Assert.Equal(PaneMode.Rows, spells.Mode);
            Assert.Equal(PaneDescriptor.Spell4.Id, State.Active);
        }

        [Fact]
        public async Task The_effect_type_menu_copies_the_type_id()
        {
            Run(Ctx.EffectTypeMenu(Usage(SpellEffectType.Damage, [1])), "Copy type id");

            await Task.Yield();
            Assert.Equal(((uint)SpellEffectType.Damage).ToString(), InvocationArgs("copyText")[0]);
        }

        [Fact]
        public void A_locked_effect_type_pane_sends_the_next_type_to_a_pane_of_its_own()
        {
            Ctx.OpenEffectTypeSpells(SpellEffectType.Damage, false);
            State.ToggleEffectTypeLock(PaneDescriptor.EffectTypeSpells.Id);

            Ctx.OpenEffectTypeSpells(SpellEffectType.Heal, false);

            Assert.Equal("effecttype:2", State.Active);
            Assert.Equal(SpellEffectType.Damage, State.EffectTypeIn(PaneDescriptor.EffectTypeSpells.Id));
        }

        // ------------------------------------------------------------------ proc menu

        [Fact]
        public void The_proc_menu_follows_through_to_the_spell_it_casts()
        {
            List<MenuItem> menu = Ctx.ProcMenu(new TestProc { SpellId = 555 });
            Assert.Equal(4, menu.Count);

            Run(menu, "Follow to spell 555");

            Assert.Equal(555u, State.SelectedSpellId);
        }

        [Fact]
        public void The_proc_menu_can_follow_into_a_new_window()
        {
            Run(Ctx.ProcMenu(new TestProc { SpellId = 555 }), "Follow in new window");

            Assert.Equal([PaneDescriptor.Detail.Id], Windows.Popped);
        }

        [Fact]
        public void The_proc_menu_can_find_the_target_in_the_browser()
        {
            Run(Ctx.ProcMenu(new TestProc { SpellId = 555 }), "Find in Spell4 browser");

            PaneState pane = State.PaneStateFor(PaneDescriptor.Spell4.Id);
            Assert.Equal("555", pane.Filters.ValueOf(FilterFields.Id));
            Assert.Equal(PaneDescriptor.Spell4.Id, State.Active);
        }

        [Fact]
        public async Task The_proc_menu_copies_the_target_spell_id()
        {
            Run(Ctx.ProcMenu(new TestProc { SpellId = 555 }), "Copy spell id");

            await Task.Yield();
            Assert.Equal("555", InvocationArgs("copyText")[0]);
        }

        // ------------------------------------------------------------------ table menu

        [Fact]
        public void The_table_menu_browses_pops_out_pins_and_copies()
        {
            List<MenuItem> menu = Ctx.TableMenu(Table("Spell4"));
            Assert.Equal(4, menu.Count);

            Run(menu, "Browse Spell4");
            Assert.Equal(PaneDescriptor.GameTableId("Spell4"), State.Active);

            Run(menu, "Browse in new window");
            Assert.Equal([PaneDescriptor.GameTableId("Spell4")], Windows.Popped);
        }

        [Fact]
        public void The_table_menu_offers_to_pin_an_unpinned_table_and_unpin_a_pinned_one()
        {
            TableDescriptor table = Table("Spell4");
            string id = PaneDescriptor.GameTableId("Spell4");

            Run(Ctx.TableMenu(table), "Promote to sidebar");
            Assert.Contains(id, State.Pinned);

            Run(Ctx.TableMenu(table), "Demote from sidebar");
            Assert.DoesNotContain(id, State.Pinned);
        }

        [Fact]
        public async Task The_table_menu_copies_the_table_file_name()
        {
            Run(Ctx.TableMenu(Table("Spell4")), "Copy table name");

            await Task.Yield();
            Assert.Equal("Spell4.tbl", InvocationArgs("copyText")[0]);
        }

        // ------------------------------------------------------------------ generic row menu

        [Fact]
        public async Task A_generic_row_can_be_copied_whole_or_by_id()
        {
            var row = new GridRow(1, ["42", "Arcane", "6"], null);
            PaneDescriptor descriptor = State.Describe(PaneDescriptor.GameTableId("Spell4"));

            List<MenuItem> menu = Ctx.GenericRowMenu(descriptor, row);
            Assert.Equal(3, menu.Count);

            Run(menu, "Copy row (tab separated)");
            await Task.Yield();
            Assert.Equal("42\tArcane\t6", InvocationArgs("copyText")[0]);
        }

        [Fact]
        public async Task A_generic_rows_id_can_be_copied_on_its_own()
        {
            // The first column is the row id on every game table, so the menu offers it separately from the
            // whole tab-separated row.
            var row = new GridRow(1, ["42", "Arcane", "6"], null);
            PaneDescriptor descriptor = State.Describe(PaneDescriptor.GameTableId("Spell4"));

            List<MenuItem> menu = Ctx.GenericRowMenu(descriptor, row);
            Assert.Contains(menu, i => i.Label == "Copy Id" && i.Hint == "42");

            Run(menu, "Copy Id");
            await Task.Yield();

            Assert.Equal("42", InvocationArgs("copyText")[0]);
        }

        [Fact]
        public void A_generic_row_can_open_its_table_in_a_new_window()
        {
            PaneDescriptor descriptor = State.Describe(PaneDescriptor.GameTableId("Spell4"));

            Run(Ctx.GenericRowMenu(descriptor, new GridRow(1, ["42"], null)), "Open Spell4 in new window");

            Assert.Equal([descriptor.Id], Windows.Popped);
        }

        // ------------------------------------------------------------------ view menu

        [Fact]
        public void A_tab_menu_offers_to_close_the_view()
        {
            List<MenuItem> menu = Ctx.ViewMenu(PaneDescriptor.Spell4.Id, fromRail: false);

            Assert.Contains(menu, i => i.Label == "Close view");
            Assert.DoesNotContain(menu, i => i.Label == "Open filter tab");
        }

        [Fact]
        public void A_rail_menu_offers_the_filter_tab_instead_of_closing()
        {
            List<MenuItem> menu = Ctx.ViewMenu(PaneDescriptor.Spell4.Id, fromRail: true);

            Assert.DoesNotContain(menu, i => i.Label == "Close view");

            Run(menu, "Open filter tab");
            Assert.Equal(PaneMode.Filter, State.PaneStateFor(PaneDescriptor.Spell4.Id).Mode);
        }

        [Fact]
        public void A_fixed_view_cannot_be_pinned_from_its_menu()
        {
            List<MenuItem> menu = Ctx.ViewMenu(PaneDescriptor.Spell4.Id, fromRail: true);

            Assert.DoesNotContain(menu, i => i.Label == "Promote to sidebar");
        }

        [Fact]
        public void A_game_table_can_be_pinned_and_demoted_from_its_menu()
        {
            string id = PaneDescriptor.GameTableId("Spell4");

            Run(Ctx.ViewMenu(id, fromRail: false), "Promote to sidebar");
            Assert.Contains(id, State.Pinned);

            Run(Ctx.ViewMenu(id, fromRail: true), "Demote to tab only");
            Assert.DoesNotContain(id, State.Pinned);
            Assert.Contains(id, State.Open);
        }

        [Fact]
        public void Every_view_menu_can_open_or_pop_out_the_view()
        {
            List<MenuItem> menu = Ctx.ViewMenu(PaneDescriptor.Tables.Id, fromRail: true);

            Run(menu, "Open view");
            Assert.Equal(PaneDescriptor.Tables.Id, State.Active);

            Run(menu, "Pop out as window");
            Assert.Equal([PaneDescriptor.Tables.Id], Windows.Popped);
        }

        // ------------------------------------------------------------------ fixtures

        private ISpellModel Spell(uint id, string description = "")
        {
            var spell = new TestSpell
            {
                Entry = new Spell4Entry { Id = id },
                Description = description,
                SpellBaseModel = new TestBase { Entry = new Spell4BaseEntry() }
            };

            Models.SpellModels[id] = spell;
            return spell;
        }

        private static ISpellEffectModel Effect(SpellEffectType type) =>
            new TestEffect { Entry = new Spell4EffectsEntry(), Type = type };

        private static TableDescriptor Table(string name) =>
            new(name, typeof(object), 1, ["Id"], () => [], _ => []);

        private sealed class TestSpell : ISpellModel
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

        private sealed class TestBase : ISpellBaseModel
        {
            public Spell4BaseEntry Entry { get; set; }
            public string Name => "";
            public Spell4HitResultsEntry HitResult => null;
            public Spell4TargetMechanicsEntry TargetMechanics { get; } = new();
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

        private sealed class TestEffect : ISpellEffectModel
        {
            public Spell4EffectsEntry Entry { get; set; }
            public SpellEffectType Type { get; set; }
            public uint TargetFlags => 0;
            public uint DamageType => 0;
            public uint DelayTime => 0;
            public uint TickTime => 0;
            public uint DurationTime => 0;
            public uint Flags => 0;
            public ISpellEffectColumnData ColumnData => null;
            public List<ISpellEffectRowData> RowData { get; } = [];

            public void Initialise(Spell4EffectsEntry entry) => Entry = entry;
        }

        private sealed class TestProc : ISpellProcModel
        {
            public Spell4EffectsEntry Entry { get; set; }
            public ProcType ProcType { get; set; }
            public uint SpellId { get; set; }

            public void Initialise(Spell4EffectsEntry entry) => Entry = entry;
        }
    }
}
