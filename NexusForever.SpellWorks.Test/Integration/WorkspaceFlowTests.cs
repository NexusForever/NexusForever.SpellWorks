using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;
using NexusForever.SpellWorks.Components;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Static;
using NexusForever.SpellWorks.Services;
using NexusForever.SpellWorks.Services.Filtering;
using NexusForever.SpellWorks.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Test.Integration
{
    /// <summary>
    /// Whole journeys through the shell, across several components and the workspace store - the flows the
    /// per-component tests each cover only one step of.
    /// </summary>
    public class WorkspaceFlowTests : ComponentTestContext
    {
        private IRenderedComponent<Shell> Shell() => RenderRoot<Shell>(p => p.Add(c => c.Bridge, Bridge));

        [Fact]
        public void A_promotion_made_in_the_form_survives_a_restart()
        {
            // The whole path, not the store on its own: the form writes the promotion into pane state,
            // the store prunes and writes it, and a fresh state reads it back against a live schema.
            string column = FilterFields.Flex(FilterFields.EffectsSource, "DataBits00");
            State.PaneStateFor(PaneDescriptor.Spell4.Id).Promoted.Add(column);

            Store.Save();

            var restored = new WorkspaceState(Messenger, Models, Catalog);
            new WorkspaceStore(restored, Schemas, StoreDirectory).Load();

            Assert.Equal([column], restored.PaneStateFor(PaneDescriptor.Spell4.Id).Promoted);
        }

        [Fact]
        public void Opening_a_table_from_the_rail_puts_a_grid_of_its_rows_on_screen()
        {
            Catalog.With("Spell4", ["Id", "Description"], [["1", "Arcane Missile"], ["2", "Healing Wave"]]);

            IRenderedComponent<Shell> cut = Shell();

            // Rail -> the table list -> a row -> that table's own grid.
            On(cut, "button.rail-item[title='Game tables']", e => e.Click());
            cut.WaitForState(() => DataRows(cut).Count > 0);

            OnRow(cut, 0, row => row.Click(new MouseEventArgs { Detail = 1 }));

            Assert.Equal(PaneDescriptor.GameTableId("Spell4"), State.Active);
            cut.WaitForAssertion(() => Assert.Contains("Arcane Missile", cut.Markup));
        }

        [Fact]
        public void A_tab_opened_reordered_and_resized_survives_a_restart()
        {
            Catalog.With("Spell4", ["Id", "Description"], [["1", "Arcane Missile"]]);
            IRenderedComponent<Shell> cut = Shell();

            // Open a third view, drag its tab to the front, then widen a column in it.
            On(cut, "button.rail-item[title='Game tables']", e => e.Click());
            cut.InvokeAsync(() => cut.Instance.OnTabReordered(PaneDescriptor.Tables.Id, 0)).GetAwaiter().GetResult();
            State.SetColumnWidth(PaneDescriptor.Tables.Id, "Table", 420);
            Store.Save();

            // A fresh workspace reading the same folder is what the next start sees.
            var restored = new WorkspaceState(Messenger, Models, Catalog);
            new WorkspaceStore(restored, Schemas, StoreDirectory).Load();

            Assert.Equal(PaneDescriptor.Tables.Id, restored.Open[0]);
            Assert.Equal(PaneDescriptor.Tables.Id, restored.Active);
            Assert.Equal(420, restored.ColumnWidth(PaneDescriptor.Tables.Id, new GridColumn("Table", "", 260)));
        }

        [Fact]
        public void Split_panes_and_a_dragged_seam_survive_a_restart()
        {
            IRenderedComponent<Shell> cut = Shell();

            OnNth(cut, "div.segmented button", 1, e => e.Click());
            On(cut, "div.pane-splitter", e => e.PointerDown(new PointerEventArgs { ClientX = 500 }));
            cut.InvokeAsync(() => cut.Instance.OnSplitDragged([2.5, 0.5])).GetAwaiter().GetResult();
            Store.Save();

            var restored = new WorkspaceState(Messenger, Models, Catalog);
            new WorkspaceStore(restored, Schemas, StoreDirectory).Load();

            Assert.Equal(LayoutMode.SplitPanes, restored.Layout);
            Assert.Equal(2.5, restored.FlexOf(State.Open[0]));
            Assert.Equal(0.5, restored.FlexOf(State.Open[1]));
        }

        [Fact]
        public void A_resized_column_is_applied_to_the_grid_the_next_time_it_renders()
        {
            Catalog.With("Spell4", ["Id"], [["1"]]);
            IRenderedComponent<Shell> cut = Shell();
            On(cut, "button.rail-item[title='Game tables']", e => e.Click());
            cut.WaitForState(() => cut.FindAll("table.grid col").Count > 0);

            State.SetColumnWidth(PaneDescriptor.Tables.Id, "Table", 420);

            cut.WaitForAssertion(() =>
                Assert.Equal("width:420px", cut.FindAll("table.grid col")[0].GetAttribute("style")));
        }

        [Fact]
        public void Pinning_a_table_puts_it_on_the_rail_and_keeps_it_there()
        {
            Catalog.With("Spell4Effects", ["Id"], [["1"]]);
            IRenderedComponent<Shell> cut = Shell();
            string id = PaneDescriptor.GameTableId("Spell4Effects");

            cut.InvokeAsync(() => cut.Instance.OnPinDropped(id, "pin")).GetAwaiter().GetResult();

            Assert.Contains("PINNED", cut.Markup);
            Assert.NotNull(cut.Find("button.rail-item[title='Spell4Effects.tbl']"));

            var restored = new WorkspaceState(Messenger, Models, Catalog);
            new WorkspaceStore(restored, Schemas, StoreDirectory).Load();
            Assert.Equal([id], restored.Pinned);
        }

        [Fact]
        public void Filtering_a_view_narrows_its_grid_and_shows_the_count_on_its_tab()
        {
            Catalog.With("Spell4", ["Id"], []);
            Catalog.With("Creature2", ["Id"], []);
            IRenderedComponent<Shell> cut = Shell();

            On(cut, "button.rail-item[title='Game tables']", e => e.Click());
            cut.WaitForState(() => DataRows(cut).Count == 2);

            // Switch the pane to its filter form, constrain it, and apply.
            OnNth(cut, "div.pane-head .segmented button", 1, e => e.Click());
            SetField(cut, FilterFields.TableName, "Spell");
            On(cut, "button.apply-btn", e => e.Click());

            cut.WaitForAssertion(() => Assert.Equal(1, DataRows(cut).Count));
            Assert.Contains("badge", cut.FindAll("div.tab")[^1].InnerHtml);
        }

        [Fact]
        public void Popping_a_view_out_takes_it_off_the_tab_strip_and_docking_brings_it_back()
        {
            IRenderedComponent<Shell> cut = Shell();
            int tabs = cut.FindAll("div.tab").Count;

            On(cut, "button.accent-btn", e => e.Click());

            Assert.Equal(tabs - 1, cut.FindAll("div.tab").Count);
            Assert.Equal(1, Popouts.OpenCount);

            // Docking is the pop-out window's job; the workspace is what both windows share.
            State.UnregisterPopout(State.Popouts[0].Key, dockBack: true);

            Assert.Equal(tabs, cut.FindAll("div.tab").Count);
        }

        [Fact]
        public void Choosing_a_spell_in_the_browser_drives_the_detail_view_beside_it()
        {
            Catalog.With("Spell4", ["Id"], []);
            IRenderedComponent<Shell> cut = Shell();

            // Both panes visible at once is the case the redesign exists for.
            OnNth(cut, "div.segmented button", 1, e => e.Click());
            State.Select(42);

            cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("section.pane").Count));
            Assert.Equal(42u, State.SelectedIn(PaneDescriptor.Detail.Id));
        }

        [Fact]
        public void A_locked_pane_keeps_its_spell_while_the_browser_moves_on()
        {
            IRenderedComponent<Shell> cut = Shell();

            // The lock lives on the Detail pane's sub-tab bar, so that view has to be the active one.
            On(cut, "button.rail-item[title='Spell detail']", e => e.Click());
            State.Select(10);

            On(cut, "button.icon-btn[title='Lock this pane to the current spell']", e => e.Click());
            State.Select(20);

            Assert.Equal(10u, State.SelectedIn(PaneDescriptor.Detail.Id));
            Assert.Equal(20u, State.SelectedSpellId);
        }

        [Fact]
        public void A_locked_pane_keeps_its_spell_and_the_next_one_opens_beside_it()
        {
            // With the Detail pane locked, opening another spell must not switch to the locked pane, which
            // still shows its own spell - the double-click would look like it did nothing.
            IRenderedComponent<Shell> cut = Shell();

            On(cut, "button.rail-item[title='Spell detail']", e => e.Click());
            State.Select(10);
            On(cut, "button.icon-btn[title='Lock this pane to the current spell']", e => e.Click());

            // What double-clicking a row in the Spell4 browser asks for.
            State.OpenDetail(20, DetailSubTab.Effects);

            Assert.Equal("detail:2", State.Active);
            Assert.Equal(10u, State.SelectedIn(PaneDescriptor.Detail.Id));
            Assert.Equal(20u, State.SelectedIn("detail:2"));

            // Both panes are on the strip, and the new one names itself apart from the locked one.
            cut.WaitForAssertion(() =>
            {
                string[] tabs = [.. cut.FindAll("div.tab button.tab-select").Select(t => t.TextContent.Trim())];
                Assert.Contains("Spell detail", tabs);
                Assert.Contains("Spell detail 2", tabs);
            });
        }

        [Fact]
        public void A_spawned_detail_pane_survives_a_restart()
        {
            Shell();

            State.Select(10);
            State.PaneStateFor(PaneDescriptor.Detail.Id).LockedSpellId = 10;
            State.OpenDetail(20);
            Store.Save();

            var restored = new WorkspaceState(Messenger, Models, Catalog);
            new WorkspaceStore(restored, Schemas, StoreDirectory).Load();

            Assert.Contains("detail:2", restored.Open);
            Assert.Equal("detail:2", restored.Active);
            Assert.Equal(PaneKind.Detail, restored.Describe("detail:2").Kind);
        }

        [Fact]
        public void An_effect_type_leads_to_its_spells_and_on_to_one_spell_s_own_detail_pane()
        {
            // The whole reverse lookup, end to end: rail -> a type -> the spells using it -> one spell.
            Models.EffectTypeUsages[SpellEffectType.Damage] = new EffectTypeUsage
            {
                Type = SpellEffectType.Damage, SpellIds = [11], EffectRowCount = 1
            };
            Models.SpellModels[11] = new TestSpell
            {
                Entry          = new Spell4Entry { Id = 11 },
                Description    = "Arcane Missile",
                SpellBaseModel = new TestBase { Entry = new Spell4BaseEntry() }
            };

            IRenderedComponent<Shell> cut = Shell();

            On(cut, "button.rail-item[title='Effect Types']", e => e.Click());
            cut.WaitForState(() => DataRows(cut).Count > 0);
            OnRow(cut, 0, row => row.Click(new MouseEventArgs { Detail = 2 }));

            Assert.Equal(PaneDescriptor.EffectTypeSpells.Id, State.Active);

            // The description is what makes the list readable, so it has to survive the hop.
            cut.WaitForAssertion(() => Assert.Contains("Arcane Missile", cut.Markup));

            OnRow(cut, 0, row => row.Click(new MouseEventArgs { Detail = 2 }));

            Assert.Equal(PaneDescriptor.Detail.Id, State.Active);
            Assert.Equal(11u, State.SelectedSpellId);

            // Both panes stand: following the spell did not consume the list it came from.
            Assert.Contains(PaneDescriptor.EffectTypes.Id, State.Open);
            Assert.Contains(PaneDescriptor.EffectTypeSpells.Id, State.Open);
            Assert.Equal(SpellEffectType.Damage, State.EffectTypeIn(PaneDescriptor.EffectTypeSpells.Id));
        }

        [Fact]
        public void A_locked_effect_type_pane_and_the_one_it_spawned_both_survive_a_restart()
        {
            State.SelectEffectType(SpellEffectType.Damage);
            State.PaneStateFor(PaneDescriptor.EffectTypeSpells.Id).LockedEffectType = SpellEffectType.Damage;
            State.OpenEffectTypeSpells(SpellEffectType.Heal);
            Store.Save();

            var restored = new WorkspaceState(Messenger, Models, Catalog);
            new WorkspaceStore(restored, Schemas, StoreDirectory).Load();

            Assert.Contains("effecttype:2", restored.Open);
            Assert.Equal("effecttype:2", restored.Active);
            Assert.Equal(PaneKind.EffectTypeSpells, restored.Describe("effecttype:2").Kind);
        }

        [Fact]
        public void Changing_the_patch_path_reloads_the_engine_and_rebuilds_the_catalog()
        {
            Engine.PatchPath = @"C:\WildStar\Patch";
            IRenderedComponent<Shell> cut = Shell();

            On(cut, "button.rail-item[title='Setup']", e => e.Click());
            On(cut, "input", e => e.Change(@"D:\Elsewhere"));
            On(cut, "button.apply-path-btn", e => e.Click());

            cut.WaitForAssertion(() => Assert.Contains(@"D:\Elsewhere", Engine.Reloads));
            Assert.True(File.Exists(Path.Combine(StoreDirectory, "Configuration.json")));
        }

        private static IReadOnlyList<IElement> DataRows(IRenderedComponent<Shell> cut) =>
            cut.FindAll("tbody tr").Where(r => r.QuerySelector("td") != null).ToList();

        private static void OnRow(IRenderedComponent<Shell> cut, int index, Action<IElement> fire) =>
            cut.InvokeAsync(() => fire(DataRows(cut)[index])).GetAwaiter().GetResult();
    }
}
