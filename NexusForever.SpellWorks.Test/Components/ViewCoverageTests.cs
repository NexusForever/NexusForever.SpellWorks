using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;
using NexusForever.SpellWorks.Components;
using NexusForever.SpellWorks.Components.Views;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Static;
using NexusForever.SpellWorks.Services;
using NexusForever.SpellWorks.Services.Filtering;
using NexusForever.SpellWorks.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Test.Components
{
    /// <summary>
    /// The filter form's field sets, one per view.
    /// </summary>
    public class FilterViewFieldTests : ComponentTestContext
    {
        private readonly PaneState _pane = new();

        private IRenderedComponent<FilterView> Form(PaneDescriptor descriptor) =>
            RenderUnderContext<FilterView>(Context(), p => p
                .Add(c => c.Descriptor, descriptor)
                .Add(c => c.Pane, _pane)
                .Add(c => c.SpellId, 0u)
                .Add(c => c.Applied, () => { }));

        private static PaneDescriptor GameTable(string name) =>
            new(PaneDescriptor.GameTableId(name), PaneKind.GameTable, name + ".tbl", name, "icon", "meta", name);

        [Theory]
        [InlineData(PaneKind.Effects)]
        [InlineData(PaneKind.Procs)]
        [InlineData(PaneKind.EffectTypes)]
        [InlineData(PaneKind.EffectTypeSpells)]
        [InlineData(PaneKind.Tables)]
        [InlineData(PaneKind.GameTable)]
        [InlineData(PaneKind.Spell4)]
        public void Every_grid_view_offers_a_form(PaneKind kind)
        {
            PaneDescriptor descriptor = kind == PaneKind.GameTable
                ? GameTable("Spell4")
                : PaneDescriptor.AllFixed.First(v => v.Kind == kind);

            IRenderedComponent<FilterView> cut = Form(descriptor);

            Assert.NotEmpty(cut.FindAll("label.field"));
            Assert.NotEmpty(cut.FindAll(".group-card"));
        }

        [Fact]
        public void The_effects_form_constrains_type_flags_tick_and_duration()
        {
            IRenderedComponent<FilterView> cut = Form(PaneDescriptor.Effects);

            SetField(cut, FilterFields.EffectFlags, "6");
            SetField(cut, FilterFields.EffectTick, "100");
            SetField(cut, FilterFields.EffectDuration, "5000");

            Assert.Equal("6", _pane.Filters.ValueOf(FilterFields.EffectFlags));
            Assert.Equal("100", _pane.Filters.ValueOf(FilterFields.EffectTick));
            Assert.Equal("5000", _pane.Filters.ValueOf(FilterFields.EffectDuration));
            Assert.Equal(3, _pane.Filters.ConditionCount);
        }

        [Fact]
        public void The_procs_form_constrains_type_target_and_references()
        {
            IRenderedComponent<FilterView> cut = Form(PaneDescriptor.Procs);

            SetField(cut, FilterFields.ProcType, "2");
            SetField(cut, FilterFields.ProcSpellId, "555");
            ClickField(cut, FilterFields.ProcReferenced, "button.toggle");

            Assert.Equal("2", _pane.Filters.ValueOf(FilterFields.ProcType));
            Assert.Equal("555", _pane.Filters.ValueOf(FilterFields.ProcSpellId));
            Assert.True(_pane.Filters.Has(FilterFields.ProcReferenced));
        }

        [Fact]
        public void The_table_list_form_constrains_the_name_and_hides_empty_tables()
        {
            IRenderedComponent<FilterView> cut = Form(PaneDescriptor.Tables);

            SetField(cut, FilterFields.TableName, "Spell");
            ClickField(cut, FilterFields.TableLoaded, "button.toggle");

            Assert.Equal("Spell", _pane.Filters.ValueOf(FilterFields.TableName));
            Assert.True(_pane.Filters.Has(FilterFields.TableLoaded));
        }

        [Fact]
        public void A_game_table_form_constrains_the_id_content_and_flags()
        {
            IRenderedComponent<FilterView> cut = Form(GameTable("Spell4"));

            SetField(cut, FilterFields.RowId, "12");
            SetField(cut, FilterFields.RowContains, "arcane");
            SetField(cut, FilterFields.RowFlags, "0x06");
            ClickField(cut, FilterFields.RowNonZero, "button.toggle");

            Assert.Equal("12", _pane.Filters.ValueOf(FilterFields.RowId));
            Assert.Equal("arcane", _pane.Filters.ValueOf(FilterFields.RowContains));
            Assert.Equal("0x06", _pane.Filters.ValueOf(FilterFields.RowFlags));
            Assert.True(_pane.Filters.Has(FilterFields.RowNonZero));
        }

        [Fact]
        public void Each_views_chips_reflect_only_its_own_constraints()
        {
            // The key belongs to the Effects schema, so the Tables form cannot render or chip it.
            _pane.Filters.Set(FilterFields.EffectFlags, "6", FilterOperator.MaskAll);

            Assert.Single(Form(PaneDescriptor.Effects).FindAll("button.chip"));
            Assert.Empty(Form(PaneDescriptor.Tables).FindAll("button.chip"));
        }
    }

    /// <summary>
    /// What the grid does with a spell, effect or proc row, as opposed to a table row.
    /// </summary>
    public class TableViewRowTests : ComponentTestContext
    {
        private readonly PaneState _pane = new();
        private ShellContext _ctx;

        private IRenderedComponent<TableView> Grid(PaneDescriptor descriptor, uint spellId = 0, bool expectRows = true)
        {
            _ctx = Context();

            IRenderedComponent<TableView> cut = RenderUnderContext<TableView>(_ctx, p => p
                .Add(c => c.Descriptor, descriptor)
                .Add(c => c.Scope, descriptor.Id)
                .Add(c => c.Pane, _pane)
                .Add(c => c.SpellId, spellId)
                .Add(c => c.Generation, 0));

            // The projection runs off the render thread, and the grid shows its empty state until it
            // lands - so waiting for "rows or empty" would race. Wait for what the test actually needs.
            if (expectRows)
                cut.WaitForState(() => DataRows(cut).Count > 0);
            else
                cut.WaitForState(() => cut.FindAll("thead th").Count > 0 || cut.FindAll(".empty-state").Count > 0);

            return cut;
        }

        private static IReadOnlyList<IElement> DataRows(IRenderedComponent<TableView> cut) =>
            cut.FindAll("tbody tr").Where(r => r.QuerySelector("td") != null).ToList();

        /// <summary>
        /// Fire an event on a data row inside one dispatcher turn. The grid projects on a background
        /// thread, so a re-render can land between finding the row and firing, invalidating the handler.
        /// </summary>
        private static void OnRow(IRenderedComponent<TableView> cut, int index, Action<IElement> fire) =>
            cut.InvokeAsync(() => fire(DataRows(cut)[index])).GetAwaiter().GetResult();

        [Fact]
        public void Clicking_a_spell_row_selects_that_spell()
        {
            Spell(7, "Arcane Missile");
            IRenderedComponent<TableView> cut = Grid(PaneDescriptor.Spell4);

            OnRow(cut, 0, row => row.Click(new MouseEventArgs { Detail = 1 }));

            Assert.Equal(7u, State.SelectedSpellId);
        }

        [Fact]
        public void Double_clicking_a_spell_row_opens_it_in_the_detail_view()
        {
            Spell(7, "Arcane Missile");
            IRenderedComponent<TableView> cut = Grid(PaneDescriptor.Spell4);

            OnRow(cut, 0, row => row.Click(new MouseEventArgs { Detail = 2 }));

            Assert.Equal(PaneDescriptor.Detail.Id, State.Active);
            Assert.Equal(7u, State.SelectedSpellId);
        }

        [Fact]
        public void The_selected_spell_is_marked_in_the_grid()
        {
            Spell(7, "Arcane Missile");
            State.Select(7);

            IRenderedComponent<TableView> cut = Grid(PaneDescriptor.Spell4);

            Assert.Contains("sel", DataRows(cut)[0].ClassName);
        }

        [Fact]
        public void Right_clicking_a_spell_row_opens_the_spell_menu()
        {
            Spell(7, "Arcane Missile");
            IRenderedComponent<TableView> cut = Grid(PaneDescriptor.Spell4);

            OnRow(cut, 0, row => row.ContextMenu(new MouseEventArgs { ClientX = 10, ClientY = 10 }));

            Assert.Equal("Arcane Missile", _ctx.Menu.Title);
            Assert.Contains("Spell4 · 7", _ctx.Menu.Sub);
        }

        [Fact]
        public void Right_clicking_an_effect_row_opens_the_effect_menu()
        {
            ISpellModel spell = Spell(7);
            AddEffect(spell, SpellEffectType.Damage);

            IRenderedComponent<TableView> cut = Grid(PaneDescriptor.Effects, 7);

            OnRow(cut, 0, row => row.ContextMenu(new MouseEventArgs()));

            Assert.Equal(nameof(SpellEffectType.Damage), _ctx.Menu.Title);
        }

        [Fact]
        public void Double_clicking_an_effect_row_opens_the_effects_section()
        {
            ISpellModel spell = Spell(7);
            AddEffect(spell, SpellEffectType.Damage);

            IRenderedComponent<TableView> cut = Grid(PaneDescriptor.Effects, 7);

            OnRow(cut, 0, row => row.Click(new MouseEventArgs { Detail = 2 }));

            Assert.Equal(DetailSubTab.Effects, State.PaneStateFor(PaneDescriptor.Detail.Id).SubTab);
        }

        [Fact]
        public void Clicking_a_proc_row_selects_the_spell_the_proc_casts()
        {
            // A proc row stands for the spell it casts, so selecting it follows through to that spell.
            Spell(555, "Proc Target");
            ISpellModel spell = Spell(7);
            ((TestSpell)spell).Procs.Add(new TestProc { SpellId = 555 });

            IRenderedComponent<TableView> cut = Grid(PaneDescriptor.Procs, 7);

            OnRow(cut, 0, row => row.Click(new MouseEventArgs { Detail = 1 }));

            Assert.Equal(555u, State.SelectedSpellId);
        }

        [Fact]
        public void Right_clicking_a_proc_row_opens_the_proc_menu()
        {
            ISpellModel spell = Spell(7);
            ((TestSpell)spell).Procs.Add(new TestProc { SpellId = 555 });

            IRenderedComponent<TableView> cut = Grid(PaneDescriptor.Procs, 7);

            OnRow(cut, 0, row => row.ContextMenu(new MouseEventArgs()));

            Assert.Contains("555", _ctx.Menu.Title);
        }

        [Fact]
        public void Double_clicking_a_proc_row_follows_to_the_spell_it_casts()
        {
            Spell(555, "Proc Target");
            ISpellModel spell = Spell(7);
            ((TestSpell)spell).Procs.Add(new TestProc { SpellId = 555 });

            IRenderedComponent<TableView> cut = Grid(PaneDescriptor.Procs, 7);

            OnRow(cut, 0, row => row.Click(new MouseEventArgs { Detail = 2 }));

            Assert.Equal(555u, State.SelectedSpellId);
        }

        [Fact]
        public void A_generic_table_row_opens_a_menu_naming_the_table()
        {
            Catalog.With("Spell4Effects", ["Id", "SpellId"], [["1", "100"]]);
            PaneDescriptor descriptor = State.Describe(PaneDescriptor.GameTableId("Spell4Effects"));

            IRenderedComponent<TableView> cut = Grid(descriptor);

            OnRow(cut, 0, row => row.ContextMenu(new MouseEventArgs()));

            Assert.Contains("Spell4Effects.tbl", _ctx.Menu.Title);
        }

        [Fact]
        public async Task Double_clicking_a_generic_row_copies_it()
        {
            Catalog.With("Spell4Effects", ["Id", "SpellId"], [["1", "100"]]);
            PaneDescriptor descriptor = State.Describe(PaneDescriptor.GameTableId("Spell4Effects"));

            IRenderedComponent<TableView> cut = Grid(descriptor);

            OnRow(cut, 0, row => row.Click(new MouseEventArgs { Detail = 2 }));

            await Task.Yield();
            Assert.Equal("1\t100", InvocationArgs("copyText")[0]);
        }

        [Fact]
        public void Each_search_box_says_what_it_matches()
        {
            string markup = Grid(PaneDescriptor.Spell4, expectRows: false).Markup;

            Assert.Contains("Search description or name…", markup);
            Assert.Contains("Spell id…", markup);
        }

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

        private static void AddEffect(ISpellModel spell, SpellEffectType type) =>
            spell.Effects.Add(new TestEffect { Entry = new Spell4EffectsEntry(), Type = type });

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
