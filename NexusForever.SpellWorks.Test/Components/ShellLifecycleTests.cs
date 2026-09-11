using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using NexusForever.SpellWorks.Components;
using NexusForever.SpellWorks.Core.Services;
using NexusForever.SpellWorks.Services;
using NexusForever.SpellWorks.Services.Filtering;
using NexusForever.SpellWorks.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Test.Components
{
    /// <summary>
    /// The shell's own lifecycle: the boot it runs on first render, the reload the setup view asks for,
    /// and the keyboard.
    /// </summary>
    public class ShellLifecycleTests : ComponentTestContext
    {
        private IRenderedComponent<Shell> Shell() => RenderRoot<Shell>(p => p.Add(c => c.Bridge, Bridge));

        [Fact]
        public void An_idle_engine_is_loaded_as_soon_as_the_shell_appears()
        {
            Engine.State = EngineState.Idle;
            Engine.PatchPath = @"C:\WildStar\Patch";

            IRenderedComponent<Shell> cut = Shell();

            cut.WaitForAssertion(() => Assert.Equal([@"C:\WildStar\Patch"], Engine.Reloads));
        }

        [Fact]
        public void An_engine_that_is_already_loaded_is_left_alone()
        {
            Engine.State = EngineState.Ready;

            Shell();

            Assert.Empty(Engine.Reloads);
        }

        [Fact]
        public void The_load_can_be_turned_off_for_start_up()
        {
            Engine.State = EngineState.Idle;
            State.Preferences.LoadOnStart = false;

            Shell();

            Assert.Empty(Engine.Reloads);
        }

        [Fact]
        public void The_boot_applies_the_saved_locale_and_rebuilds_the_search_index()
        {
            Engine.State = EngineState.Idle;
            State.Preferences.Locale = "deDE";
            Catalog.With("Spell4", ["Id"], []);

            IRenderedComponent<Shell> cut = Shell();

            cut.WaitForAssertion(() => Assert.Equal("deDE", Text.Locale));
            Assert.Contains(Palette.Search("spell4"), e => e.Kind == "table");
        }

        [Fact]
        public void The_locale_is_applied_before_the_load_describes_what_it_read()
        {
            // The engine stamps the archive's text table and string count into Info as the load finishes,
            // reading them from whichever locale the text service is pointing at. Applying the preference
            // after that stamped the wrong one into Setup on every single load - the previous locale, or
            // the archive's first, which is what a fresh profile sees for ever.
            Engine.State = EngineState.Idle;
            Engine.Gate = new TaskCompletionSource();
            State.Preferences.Locale = "deDE";

            IRenderedComponent<Shell> cut = Shell();

            try
            {
                cut.WaitForState(() => Engine.Reloads.Count > 0);

                // Still mid-load, and the text service already knows which locale it is reading.
                Assert.Equal("deDE", Text.Locale);
            }
            finally
            {
                Engine.Gate.SetResult();
            }
        }

        [Fact]
        public void The_boot_restores_the_windows_that_were_open_last_time()
        {
            Engine.State = EngineState.Idle;
            State.RegisterPopout("key", PaneDescriptor.Effects.Id);
            Store.Save();

            // The boot restores from what the store read back off disk, so the saved workspace has to be
            // loaded into the store the shell actually resolves - a second instance is not the one it uses.
            State.UnregisterPopout("key", false);
            Store.Load();

            IRenderedComponent<Shell> cut = Shell();

            cut.WaitForAssertion(() => Assert.Equal([PaneDescriptor.Effects.Id], Windows.Popped));
        }

        [Fact]
        public void Restoring_windows_can_be_turned_off()
        {
            Engine.State = EngineState.Idle;
            State.Preferences.RestoreWindows = false;

            IRenderedComponent<Shell> cut = Shell();

            cut.WaitForAssertion(() => Assert.NotEmpty(Engine.Reloads));
            Assert.Empty(Windows.Popped);
        }

        [Fact]
        public void A_boot_that_finishes_after_the_window_is_gone_opens_nothing()
        {
            // Reading the archive takes seconds, and the window can be closed while the boot scrim is up.
            // The continuation then still ran: it restored the pop-outs, which builds WPF windows - after
            // the last window closed, which is after the application has begun shutting down.
            Engine.State = EngineState.Idle;
            Engine.Gate = new TaskCompletionSource();
            State.Preferences.RestoreWindows = true;

            WorkspaceState saved = new(Messenger, Models, Catalog);
            saved.RegisterPopout("key", "effects");
            new WorkspaceStore(saved, Schemas, StoreDirectory).Save();
            Store.Load();

            IRenderedComponent<Shell> cut = Shell();
            cut.WaitForState(() => Engine.Reloads.Count > 0);

            DisposeComponentsAsync().GetAwaiter().GetResult();

            Engine.Gate.SetResult();

            // Give the continuation every chance to run before concluding that it did not open anything.
            Thread.Sleep(250);

            Assert.Empty(Windows.Popped);
        }

        // ------------------------------------------------------------------ the boot overlay

        /// <summary>
        /// A shell whose boot is held open on <paramref name="reports"/>, so the overlay can be looked at
        /// before the load is allowed to finish.
        /// </summary>
        private IRenderedComponent<Shell> Booting(params EngineProgress[] reports)
        {
            Engine.State = EngineState.Idle;
            Engine.Reports.AddRange(reports);
            Engine.Gate = new TaskCompletionSource();

            return Shell();
        }

        private void FinishBoot(IRenderedComponent<Shell> cut)
        {
            Engine.Gate.SetResult();
            cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("div.boot")));
        }

        [Fact]
        public void The_boot_overlay_reports_how_far_the_load_has_got()
        {
            IRenderedComponent<Shell> cut = Booting(new EngineProgress("Reading Spell4…", 25, 0, 100));

            try
            {
                cut.WaitForAssertion(() => Assert.Equal("Reading Spell4…", cut.Find("div.boot .msg").TextContent));
                Assert.Contains("width:25%", cut.Find("div.boot-bar .fill").GetAttribute("style"));
                Assert.Equal(Engine.PatchPath, cut.Find("div.boot .detail").TextContent);
            }
            finally
            {
                FinishBoot(cut);
            }
        }

        [Fact]
        public void A_boot_that_has_not_reported_yet_says_what_it_is_doing_and_shows_no_progress()
        {
            // The engine reports a message long before it can say how much of the archive is left.
            IRenderedComponent<Shell> cut = Booting(new EngineProgress());

            try
            {
                cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("div.boot")));
                Assert.Equal("Reading client archive…", cut.Find("div.boot .msg").TextContent);
                Assert.Contains("width:0%", cut.Find("div.boot-bar .fill").GetAttribute("style"));
            }
            finally
            {
                FinishBoot(cut);
            }
        }

        [Fact]
        public void A_boot_whose_range_is_empty_shows_no_progress_rather_than_dividing_by_it()
        {
            IRenderedComponent<Shell> cut = Booting(new EngineProgress("Mounting", 5, 5, 5));

            try
            {
                cut.WaitForAssertion(() => Assert.Equal("Mounting", cut.Find("div.boot .msg").TextContent));
                Assert.Contains("width:0%", cut.Find("div.boot-bar .fill").GetAttribute("style"));
            }
            finally
            {
                FinishBoot(cut);
            }
        }

        [Fact]
        public void A_burst_of_progress_does_not_cost_a_render_apiece()
        {
            // The real load reports per table row. Rendering each one floods the queue, so reports inside
            // the 80 ms window update the state without asking for a frame.
            EngineProgress[] burst = [.. Enumerable
                .Range(0, 200)
                .Select(i => new EngineProgress($"Reading {i}", i, 0, 200))];

            IRenderedComponent<Shell> cut = Booting(burst);

            try
            {
                cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("div.boot")));

                Assert.True(cut.RenderCount < burst.Length,
                    $"the shell rendered {cut.RenderCount} times for {burst.Length} progress reports");
            }
            finally
            {
                FinishBoot(cut);
            }
        }

        [Fact]
        public void The_setup_view_can_ask_for_a_reload_against_a_new_path()
        {
            State.SelectView(PaneDescriptor.Setup.Id);
            State.PathDraft = @"D:\Elsewhere";
            IRenderedComponent<Shell> cut = Shell();

            On(cut, "button.apply-path-btn", e => e.Click());

            cut.WaitForAssertion(() => Assert.Contains(@"D:\Elsewhere", Engine.Reloads));
        }

        [Fact]
        public void A_boot_that_cannot_read_the_archive_opens_Setup_with_the_installations_listed()
        {
            // Nothing loaded means nothing to look at, so land where it can be put right.
            Engine.State = EngineState.Idle;
            Engine.FailWith = "Could not find ClientData.archive";

            IRenderedComponent<Shell> cut = Shell();

            cut.WaitForAssertion(() => Assert.Equal(PaneDescriptor.Setup.Id, State.Active));
            Assert.True(State.IsBrowsing);
        }

        [Fact]
        public void A_boot_that_works_leaves_the_view_where_it_was()
        {
            Engine.State = EngineState.Idle;

            IRenderedComponent<Shell> cut = Shell();

            cut.WaitForAssertion(() => Assert.NotEmpty(Engine.Reloads));
            Assert.NotEqual(PaneDescriptor.Setup.Id, State.Active);
            Assert.False(State.IsBrowsing);
        }

        [Fact]
        public void A_reload_from_Setup_that_fails_opens_the_installations()
        {
            State.SelectView(PaneDescriptor.Setup.Id);
            State.PathDraft = @"D:Nowhere";
            IRenderedComponent<Shell> cut = Shell();
            Engine.FailWith = "Could not find ClientData.archive";

            On(cut, "button.apply-path-btn", e => e.Click());

            cut.WaitForAssertion(() => Assert.True(State.IsBrowsing));
        }

        // ------------------------------------------------------------------ status bar

        [Fact]
        public void The_status_bar_names_the_active_view_and_its_filter_count()
        {
            State.PaneStateFor(PaneDescriptor.Spell4.Id).Filters
                .Set(FilterFields.HasProcs, "", FilterOperator.IsSet);

            IRenderedComponent<Shell> cut = Shell();

            Assert.Contains("Spell4 browser · 1 filter(s) active", cut.Markup);
        }

        [Fact]
        public void With_nothing_open_the_status_bar_says_to_pick_a_view()
        {
            State.CloseView("spells");
            State.CloseView("detail");

            IRenderedComponent<Shell> cut = Shell();

            Assert.Contains("No view open — pick one from the rail", cut.Markup);
            Assert.Contains("empty-state", cut.Markup);
        }

        [Fact]
        public void The_status_bar_reports_the_spell_table_size_once_it_is_loaded()
        {
            Catalog.With("Spell4", ["Id"], [["1"], ["2"]]);

            IRenderedComponent<Shell> cut = Shell();

            Assert.Contains("Spell4.tbl · 2 rows", cut.Markup);
        }

        [Fact]
        public void The_status_bar_says_when_the_spell_table_is_not_loaded()
        {
            Assert.Contains("Spell4.tbl · not loaded", Shell().Markup);
        }

        [Fact]
        public void A_tab_carries_a_badge_once_its_view_has_a_filter_set()
        {
            State.PaneStateFor(PaneDescriptor.Spell4.Id).Filters
                .Set(FilterFields.HasProcs, "", FilterOperator.IsSet);

            IRenderedComponent<Shell> cut = Shell();

            Assert.Contains("badge", cut.FindAll("div.tab")[0].InnerHtml);
        }

        [Fact]
        public void The_header_reports_the_patch_path_when_no_archive_is_mounted()
        {
            Engine.Info = null;
            Engine.PatchPath = null;

            Assert.Contains("no patch path configured", Shell().Markup);
        }

        [Fact]
        public void The_header_reports_the_mounted_archive_file()
        {
            Engine.PatchPath = @"C:\WildStar\Patch";
            Engine.Info = new ArchiveInfo(@"C:\WildStar\Patch", "ClientData.archive", 1, "en-US.bin", 1,
                DateTimeOffset.UnixEpoch);

            Assert.Contains("ClientData.archive", Shell().Markup);
        }

        // ------------------------------------------------------------------ keyboard

        [Fact]
        public async Task Arrow_keys_walk_the_palette_results()
        {
            Catalog.With("Creature2", ["Id"], []);
            Palette.Rebuild();
            IRenderedComponent<Shell> cut = Shell();

            await cut.InvokeAsync(() => cut.Instance.OnHotkey("palette", false));
            await cut.InvokeAsync(() => cut.Instance.OnHotkey("down", false));

            Assert.Contains("cursor", cut.FindAll("button.palette-result")[1].ClassName);

            await cut.InvokeAsync(() => cut.Instance.OnHotkey("up", false));
            Assert.Contains("cursor", cut.FindAll("button.palette-result")[0].ClassName);
        }

        [Fact]
        public async Task Arrow_keys_wrap_around_the_palette()
        {
            Palette.Rebuild();
            IRenderedComponent<Shell> cut = Shell();
            await cut.InvokeAsync(() => cut.Instance.OnHotkey("palette", false));

            int count = cut.FindAll("button.palette-result").Count;
            await cut.InvokeAsync(() => cut.Instance.OnHotkey("up", false));

            Assert.Contains("cursor", cut.FindAll("button.palette-result")[count - 1].ClassName);
        }

        [Fact]
        public async Task Enter_opens_the_palette_hit_under_the_cursor()
        {
            Palette.Rebuild();
            IRenderedComponent<Shell> cut = Shell();
            await cut.InvokeAsync(() => cut.Instance.OnHotkey("palette", false));

            On(cut, ".palette-input input", e => e.Input("game tables"));
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll("button.palette-result")));

            await cut.InvokeAsync(() => cut.Instance.OnHotkey("enter", false));

            cut.WaitForAssertion(() => Assert.Equal(PaneDescriptor.Tables.Id, State.Active));
        }

        [Fact]
        public async Task Enter_straight_after_typing_opens_what_was_typed()
        {
            // No wait for the 150 ms search debounce: a fast typist presses Enter inside it, and the hit
            // under the cursor then still belonged to the query before the last keystroke.
            Catalog.With("Creature2", ["Id"], []);
            Palette.Rebuild();
            IRenderedComponent<Shell> cut = Shell();
            await cut.InvokeAsync(() => cut.Instance.OnHotkey("palette", false));

            On(cut, ".palette-input input", e => e.Input("creature2"));
            await cut.InvokeAsync(() => cut.Instance.OnHotkey("enter", false));

            cut.WaitForAssertion(() => Assert.Equal(PaneDescriptor.GameTableId("Creature2"), State.Active));
        }

        [Fact]
        public async Task An_arrow_key_straight_after_typing_is_not_lost()
        {
            Catalog.With("Creature2", ["Id"], []);
            Catalog.With("Creature2Display", ["Id"], []);
            Palette.Rebuild();
            IRenderedComponent<Shell> cut = Shell();
            await cut.InvokeAsync(() => cut.Instance.OnHotkey("palette", false));

            On(cut, ".palette-input input", e => e.Input("creature"));
            await cut.InvokeAsync(() => cut.Instance.OnHotkey("down", false));

            // Long enough for a debounced search still in flight to land and reset the cursor.
            await Task.Delay(400);

            cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("button.palette-result").Count));
            Assert.Contains("cursor", cut.FindAll("button.palette-result")[1].ClassName);
        }

        [Fact]
        public async Task Shift_enter_pops_the_palette_hit_out_instead()
        {
            Palette.Rebuild();
            IRenderedComponent<Shell> cut = Shell();
            await cut.InvokeAsync(() => cut.Instance.OnHotkey("palette", false));

            On(cut, ".palette-input input", e => e.Input("game tables"));
            cut.WaitForAssertion(() => Assert.Single(cut.FindAll("button.palette-result")));

            await cut.InvokeAsync(() => cut.Instance.OnHotkey("enter", true));

            cut.WaitForAssertion(() => Assert.Equal([PaneDescriptor.Tables.Id], Windows.Popped));
        }

        [Fact]
        public async Task Arrow_keys_walk_an_open_context_menu()
        {
            IRenderedComponent<Shell> cut = Shell();
            OnNth(cut, "div.tab", 0, e => e.ContextMenu(new MouseEventArgs()));
            cut.WaitForAssertion(() => Assert.Contains("menu-items", cut.Markup));

            await cut.InvokeAsync(() => cut.Instance.OnHotkey("down", false));

            Assert.Contains("cursor", cut.Find("button.menu-item").ClassName);
        }

        [Fact]
        public async Task Enter_runs_the_menu_item_under_the_cursor()
        {
            IRenderedComponent<Shell> cut = Shell();
            OnNth(cut, "div.tab", 1, e => e.ContextMenu(new MouseEventArgs()));
            cut.WaitForAssertion(() => Assert.Contains("menu-items", cut.Markup));

            await cut.InvokeAsync(() => cut.Instance.OnHotkey("down", false));
            await cut.InvokeAsync(() => cut.Instance.OnHotkey("enter", false));

            // The first item of a view menu opens the view.
            Assert.Equal("detail", State.Active);
        }

        [Fact]
        public async Task Enter_with_nothing_open_does_nothing()
        {
            IRenderedComponent<Shell> cut = Shell();

            await cut.InvokeAsync(() => cut.Instance.OnHotkey("enter", false));

            Assert.DoesNotContain("menu-items", cut.Markup);
        }

        [Fact]
        public async Task Arrow_keys_with_nothing_open_do_nothing()
        {
            IRenderedComponent<Shell> cut = Shell();

            await cut.InvokeAsync(() => cut.Instance.OnHotkey("down", false));

            Assert.DoesNotContain("palette-input", cut.Markup);
        }

        [Fact]
        public async Task Escape_with_nothing_open_does_nothing()
        {
            IRenderedComponent<Shell> cut = Shell();

            await cut.InvokeAsync(() => cut.Instance.OnHotkey("escape", false));

            Assert.DoesNotContain("palette-input", cut.Markup);
        }

        [Fact]
        public void The_hotkeys_are_registered_with_the_module_on_first_render()
        {
            Shell();

            Assert.Equal(1, InvocationCount("registerHotkeys"));
        }

        [Fact]
        public void The_shell_stops_listening_once_it_is_gone()
        {
            IRenderedComponent<Shell> cut = Shell();
            int before = State.Open.Count;

            Renderer.DisposeComponents();

            // A disposed shell must not react to another window's changes.
            State.SelectView(PaneDescriptor.Tables.Id);
            Assert.Equal(before + 1, State.Open.Count);
        }
    }
}
