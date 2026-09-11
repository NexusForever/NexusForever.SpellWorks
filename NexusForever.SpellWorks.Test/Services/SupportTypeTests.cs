using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Spell;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Static;
using NexusForever.SpellWorks.Services;
using NexusForever.SpellWorks.Services.Filtering;
using NexusForever.SpellWorks.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Test.Services
{
    /// <summary>
    /// Where a borderless window sits when maximized. The arithmetic that decides whether the taskbar
    /// stays visible.
    /// </summary>
    public class MaximisedBoundsTests
    {
        private static readonly ScreenRect Monitor = new(0, 0, 1920, 1080);
        private static readonly ScreenRect Work = new(0, 0, 1920, 1040);

        [Fact]
        public void A_maximised_window_fills_the_work_area_not_the_monitor()
        {
            (int x, int y, int width, int height) = MaximisedBounds.For(Monitor, Work, null);

            Assert.Equal(0, x);
            Assert.Equal(0, y);
            Assert.Equal(1920, width);
            Assert.Equal(1040, height);
        }

        [Fact]
        public void The_position_is_relative_to_the_monitor_so_a_second_display_works()
        {
            var monitor = new ScreenRect(1920, 0, 3840, 1080);
            var work = new ScreenRect(1920, 30, 3840, 1080);

            (int x, int y, int width, int height) = MaximisedBounds.For(monitor, work, null);

            Assert.Equal(0, x);
            Assert.Equal(30, y);
            Assert.Equal(1920, width);
            Assert.Equal(1050, height);
        }

        [Theory]
        [InlineData(TaskbarEdge.Bottom, 0, 1039)]
        [InlineData(TaskbarEdge.Top, 1, 1039)]
        [InlineData(TaskbarEdge.Left, 0, 1040)]
        [InlineData(TaskbarEdge.Right, 0, 1040)]
        public void An_auto_hiding_taskbar_keeps_a_pixel_so_it_can_reappear(TaskbarEdge edge, int y, int height)
        {
            // Covering the whole edge leaves the bar no way to be re-triggered.
            (int _, int actualY, int _, int actualHeight) = MaximisedBounds.For(Monitor, Work, edge);

            Assert.Equal(y, actualY);
            Assert.Equal(height, actualHeight);
        }

        [Fact]
        public void A_taskbar_that_does_not_auto_hide_costs_nothing()
        {
            Assert.Equal(Work, MaximisedBounds.Reserve(Work, null));
        }

        [Fact]
        public void The_reserved_pixel_comes_off_the_edge_the_bar_is_on()
        {
            Assert.Equal(new ScreenRect(1, 0, 1920, 1040), MaximisedBounds.Reserve(Work, TaskbarEdge.Left));
            Assert.Equal(new ScreenRect(0, 0, 1919, 1040), MaximisedBounds.Reserve(Work, TaskbarEdge.Right));
        }

        [Fact]
        public void A_taskbar_on_this_monitor_overlaps_it()
        {
            Assert.True(MaximisedBounds.Overlaps(Monitor, new ScreenRect(0, 1040, 1920, 1080)));
        }

        [Fact]
        public void A_taskbar_on_another_monitor_does_not()
        {
            Assert.False(MaximisedBounds.Overlaps(Monitor, new ScreenRect(1920, 1040, 3840, 1080)));
            Assert.False(MaximisedBounds.Overlaps(Monitor, new ScreenRect(0, 1080, 1920, 1120)));
        }

        [Fact]
        public void A_rectangle_reports_its_own_size()
        {
            Assert.Equal(1920, Monitor.Width);
            Assert.Equal(1080, Monitor.Height);
        }
    }

    public class EnumTextTests
    {
        [Fact]
        public void A_defined_value_renders_as_its_name()
        {
            Assert.Equal(nameof(Class.Esper), EnumText.Name<Class>((uint)Class.Esper));
        }

        [Fact]
        public void An_undefined_value_renders_as_the_number()
        {
            Assert.Equal("250", EnumText.Name<Class>(250));
        }

        [Fact]
        public void A_value_too_large_for_the_underlying_type_renders_as_the_number()
        {
            // Class is byte-backed, so a direct cast would throw rather than fall back.
            Assert.Equal("70000", EnumText.Name<Class>(70000));
        }

        [Fact]
        public void A_missing_value_renders_as_nothing()
        {
            Assert.Equal("", EnumText.Name<Class>((uint?)null));
        }

        [Fact]
        public void A_present_optional_value_renders_like_a_plain_one()
        {
            Assert.Equal(nameof(Class.Esper), EnumText.Name<Class>((uint?)(uint)Class.Esper));
        }

        [Fact]
        public void The_bits_of_a_flags_enum_are_offered_without_its_zero_member()
        {
            // The zero member names the absence of every flag, so offering it to tick would be a no-op.
            IReadOnlyList<(string Name, uint Value)> bits = EnumText.Bits<SpellTargetMechanicFlags>();

            Assert.DoesNotContain(bits, b => b.Value == 0);
            Assert.Contains(bits, b => b.Name == nameof(SpellTargetMechanicFlags.IsPlayer));
        }

        [Fact]
        public void A_mask_decomposes_into_the_names_of_the_bits_it_sets()
        {
            uint mask = (uint)(SpellTargetMechanicFlags.IsPlayer | SpellTargetMechanicFlags.IsFriendly);

            Assert.Equal("IsPlayer | IsFriendly", EnumText.Flags<SpellTargetMechanicFlags>(mask));
        }

        [Fact]
        public void An_empty_mask_reads_as_none()
        {
            Assert.Equal("None", EnumText.Flags<SpellTargetMechanicFlags>(0));
        }

        [Fact]
        public void Bits_no_enum_names_are_reported_rather_than_hidden()
        {
            // The client data carries bits nothing here names yet; hiding them would make the picker lie
            // about what the filter actually matches.
            uint mask = (uint)SpellTargetMechanicFlags.IsPlayer | 0x8000u;

            Assert.Equal("IsPlayer | 0x8000", EnumText.Flags<SpellTargetMechanicFlags>(mask));
        }
    }

    public class GridDataTests
    {
        [Fact]
        public void The_table_width_is_the_sum_of_its_columns()
        {
            var data = new GridData(
                [new GridColumn("Id", "id", 84), new GridColumn("Description", "", 560)],
                [],
                0);

            Assert.Equal(644, data.TotalWidth);
        }

        [Fact]
        public void An_empty_grid_has_no_width_and_no_rows()
        {
            Assert.Equal(0, GridData.Empty.TotalWidth);
            Assert.Empty(GridData.Empty.Rows);
            Assert.Empty(GridData.Empty.Columns);
            Assert.Equal(0, GridData.Empty.Total);
        }

        [Fact]
        public void The_total_is_the_unfiltered_count_not_the_row_count()
        {
            var data = new GridData([], [new GridRow(1, ["a"], null)], 500);

            Assert.Single(data.Rows);
            Assert.Equal(500, data.Total);
        }
    }

    public class PaneDescriptorTests
    {
        [Fact]
        public void Only_a_game_table_can_be_pinned()
        {
            Assert.False(PaneDescriptor.Spell4.CanPin);
            Assert.False(PaneDescriptor.Setup.CanPin);

            var table = new PaneDescriptor("tbl:X", PaneKind.GameTable, "X.tbl", "X", "icon", "meta", "X");
            Assert.True(table.CanPin);
        }

        [Theory]
        [InlineData(PaneKind.Spell4, true)]
        [InlineData(PaneKind.Effects, true)]
        [InlineData(PaneKind.Procs, true)]
        [InlineData(PaneKind.EffectTypes, true)]
        [InlineData(PaneKind.EffectTypeSpells, true)]
        [InlineData(PaneKind.Tables, true)]
        [InlineData(PaneKind.GameTable, true)]
        [InlineData(PaneKind.Detail, false)]
        [InlineData(PaneKind.Setup, false)]
        public void The_grid_views_are_the_table_kinds(PaneKind kind, bool isTable)
        {
            var descriptor = new PaneDescriptor("id", kind, "title", "label", "icon", "meta");

            Assert.Equal(isTable, descriptor.IsTableKind);
        }

        [Fact]
        public void A_game_table_id_is_its_name_behind_the_prefix()
        {
            Assert.Equal("tbl:Spell4Effects", PaneDescriptor.GameTableId("Spell4Effects"));
            Assert.StartsWith(PaneDescriptor.GameTablePrefix, PaneDescriptor.GameTableId("X"));
        }

        [Fact]
        public void The_rail_shows_the_browsing_views_with_setup_kept_separate()
        {
            Assert.Equal(7, PaneDescriptor.RailViews.Count);
            Assert.DoesNotContain(PaneDescriptor.Setup, PaneDescriptor.RailViews);
            Assert.Contains(PaneDescriptor.Setup, PaneDescriptor.AllFixed);
            Assert.Equal(8, PaneDescriptor.AllFixed.Count);
        }

        [Fact]
        public void Every_fixed_view_has_a_distinct_id()
        {
            string[] ids = PaneDescriptor.AllFixed.Select(v => v.Id).ToArray();

            Assert.Equal(ids.Length, ids.Distinct().Count());
        }
    }

    public class PaneStateTests
    {
        [Fact]
        public void A_pane_starts_showing_rows_with_effects_selected_and_nothing_locked()
        {
            var state = new PaneState();

            Assert.Equal(PaneMode.Rows, state.Mode);
            Assert.Equal(DetailSubTab.Effects, state.SubTab);
            Assert.Null(state.LockedSpellId);
            Assert.NotNull(state.Filters);
        }
    }

    public class ContextMenuModelTests
    {
        [Fact]
        public void A_menu_starts_unplaced_with_no_item_under_the_cursor()
        {
            var menu = new ContextMenuModel { Title = "Spell", Sub = "Spell4 · 1" };

            Assert.False(menu.Placed);
            Assert.Equal(-1, menu.Cursor);
            Assert.Empty(menu.Items);
        }

        [Fact]
        public void An_items_action_is_what_gets_invoked()
        {
            var invoked = false;
            var item = new MenuItem("icon", "Label", "hint", () => invoked = true);

            item.Invoke();

            Assert.True(invoked);
        }
    }

    public class PaletteIndexTests
    {
        private readonly FakeTableCatalog _catalog = new();
        private readonly FakeSpellModelService _models = new();

        private PaletteIndex Index()
        {
            var index = new PaletteIndex(_models, _catalog);
            index.Rebuild();
            return index;
        }

        [Fact]
        public void Indexes_every_fixed_view()
        {
            List<PaletteEntry> results = Index().Search("");

            Assert.Equal(PaneDescriptor.AllFixed.Count, results.Count(e => e.Kind == "view"));
        }

        [Fact]
        public void Indexes_every_game_table()
        {
            _catalog.With("Spell4Effects", ["Id"], [["1"]]);

            // The fixed Effects view is titled Spell4Effects too, so both are legitimate hits.
            List<PaletteEntry> hits = Index().Search("spell4effects");
            PaletteEntry table = Assert.Single(hits, e => e.Kind == "table");

            Assert.Equal("Spell4Effects.tbl", table.Label);
            Assert.Equal("1 rows · 1 columns", table.Sub);
            Assert.Equal(PaneDescriptor.GameTableId("Spell4Effects"), table.ViewId);
        }

        [Fact]
        public void Indexes_every_effect_type_the_client_uses()
        {
            _models.EffectTypeUsages[SpellEffectType.Damage] = new EffectTypeUsage
            {
                Type = SpellEffectType.Damage, SpellIds = [1, 2], EffectRowCount = 5
            };

            PaletteEntry hit = Assert.Single(Index().Search("damage"), e => e.Kind == "effecttype");

            Assert.Equal(nameof(SpellEffectType.Damage), hit.Label);
            Assert.Equal("2 spells · 5 effect rows", hit.Sub);
            Assert.Equal(PaneDescriptor.EffectTypeSpells.Id, hit.ViewId);
            Assert.Equal(SpellEffectType.Damage, hit.EffectType);
        }

        [Fact]
        public void An_effect_type_is_findable_by_its_numeric_id_too()
        {
            _models.EffectTypeUsages[SpellEffectType.Damage] = new EffectTypeUsage
            {
                Type = SpellEffectType.Damage, SpellIds = [1], EffectRowCount = 1
            };

            string id = ((uint)SpellEffectType.Damage).ToString();

            Assert.Contains(Index().Search(id), e => e.Kind == "effecttype");
        }

        [Fact]
        public void Finds_a_view_by_its_title()
        {
            Assert.Contains(Index().Search("setup"), e => e.ViewId == PaneDescriptor.Setup.Id);
        }

        [Fact]
        public void Search_ignores_case_and_surrounding_space()
        {
            _catalog.With("SomeTable", ["Id"], []);

            Assert.Single(Index().Search("  SOMETABLE  "));
        }

        [Fact]
        public void An_empty_query_lists_the_first_page()
        {
            List<PaletteEntry> results = Index().Search("   ");

            Assert.NotEmpty(results);
            Assert.True(results.Count <= PaletteIndex.MaxResults);
        }

        [Fact]
        public void A_query_that_matches_nothing_returns_nothing()
        {
            Assert.Empty(Index().Search("zzzzz-no-such-thing"));
        }

        [Fact]
        public void Results_are_capped_so_a_broad_query_stays_cheap()
        {
            for (var i = 0; i < PaletteIndex.MaxResults + 20; i++)
                _catalog.With($"Table{i}", ["Id"], []);

            Assert.Equal(PaletteIndex.MaxResults, Index().Search("table").Count);
        }

        [Fact]
        public void An_index_that_was_never_built_finds_nothing()
        {
            Assert.Empty(new PaletteIndex(_models, _catalog).Search("anything"));
        }
    }

    public sealed class FileLoggerProviderTests : IDisposable
    {
        private readonly string _directory =
            Path.Combine(Path.GetTempPath(), "SpellWorks.Log", Guid.NewGuid().ToString("n"));

        public FileLoggerProviderTests() => Directory.CreateDirectory(_directory);

        public void Dispose()
        {
            try
            {
                Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
            }
        }

        private string LogFile => Path.Combine(_directory, "SpellWorks.log");

        [Fact]
        public void A_warning_reaches_the_file()
        {
            using var provider = new FileLoggerProvider(LogFile);

            provider.CreateLogger("Blazor").LogWarning("something went wrong");

            Assert.Contains("something went wrong", File.ReadAllText(LogFile));
            Assert.Contains("Blazor", File.ReadAllText(LogFile));
        }

        [Fact]
        public void Anything_below_a_warning_is_not_worth_a_line()
        {
            // A WebView has no console, so the file is the only log - keep it to what matters.
            using var provider = new FileLoggerProvider(LogFile);
            ILogger logger = provider.CreateLogger("Blazor");

            logger.LogInformation("routine");
            logger.LogDebug("noisy");

            Assert.False(File.Exists(LogFile));
            Assert.False(logger.IsEnabled(LogLevel.Information));
            Assert.True(logger.IsEnabled(LogLevel.Error));
        }

        [Fact]
        public void An_exception_is_written_out_with_the_message()
        {
            using var provider = new FileLoggerProvider(LogFile);

            provider.CreateLogger("Blazor").LogError(new InvalidOperationException("inner detail"), "it failed");

            string log = File.ReadAllText(LogFile);
            Assert.Contains("it failed", log);
            Assert.Contains("inner detail", log);
        }

        [Fact]
        public void Lines_accumulate_rather_than_overwriting()
        {
            FileLoggerProvider.Write(LogFile, "first");
            FileLoggerProvider.Write(LogFile, "second");

            string[] lines = File.ReadAllLines(LogFile);
            Assert.Equal(2, lines.Length);
            Assert.EndsWith("first", lines[0]);
        }

        [Fact]
        public void A_line_carries_a_timestamp()
        {
            FileLoggerProvider.Write(LogFile, "entry");

            Assert.Matches(@"^\d{2}:\d{2}:\d{2}\.\d{3} entry$", File.ReadAllLines(LogFile)[0]);
        }

        [Fact]
        public void An_unwritable_path_is_swallowed_because_logging_must_not_take_the_app_down()
        {
            FileLoggerProvider.Write(Path.Combine(_directory, "no", "such", "folder", "x.log"), "entry");
        }

        [Fact]
        public void A_logger_has_no_scopes_to_open()
        {
            using var provider = new FileLoggerProvider(LogFile);

            Assert.Null(provider.CreateLogger("Blazor").BeginScope("scope"));
        }
    }

    /// <summary>
    /// Deferred work that a newer call supersedes. The two ways a superseded run has to be dropped are
    /// what this exists for, and neither is reachable from the components that use it.
    /// </summary>
    public class DebounceTests
    {
        [Fact]
        public async Task Work_runs_after_the_pause_and_its_result_is_committed()
        {
            using var debounce = new Debounce(TimeSpan.Zero);
            int committed = 0;

            await debounce.Run(_ => Task.FromResult(7), value => committed = value);

            Assert.Equal(7, committed);
        }

        [Fact]
        public async Task A_run_cancelled_while_it_waits_never_starts_its_work()
        {
            using var debounce = new Debounce(TimeSpan.FromSeconds(30));
            var ran = false;

            Task run = debounce.Run(_ => { ran = true; return Task.FromResult(1); }, _ => { });
            debounce.Cancel();
            await run;

            Assert.False(ran);
        }

        [Fact]
        public async Task A_run_overtaken_while_its_work_was_running_never_commits()
        {
            // The other half, and the one a component test cannot aim at: the projection ran to completion,
            // but a newer keystroke started while it did, so its answer is already stale. Committing it
            // would put the older result on screen and leave it there.
            using var debounce = new Debounce(TimeSpan.Zero);
            var committed = false;

            await debounce.Run(
                _ =>
                {
                    debounce.Cancel();
                    return Task.FromResult(1);
                },
                _ => committed = true);

            Assert.False(committed);
        }

        [Fact]
        public async Task A_newer_run_is_the_one_that_lands()
        {
            using var debounce = new Debounce(TimeSpan.FromMilliseconds(50));
            List<int> committed = [];

            Task first = debounce.Run(_ => Task.FromResult(1), committed.Add);
            Task second = debounce.Run(_ => Task.FromResult(2), committed.Add);

            await Task.WhenAll(first, second);

            Assert.Equal([2], committed);
        }

        [Fact]
        public async Task Disposing_abandons_what_was_in_flight()
        {
            using var debounce = new Debounce(TimeSpan.FromSeconds(30));
            var committed = false;

            Task run = debounce.Run(_ => Task.FromResult(1), _ => committed = true);
            debounce.Dispose();
            await run;

            Assert.False(committed);
        }

        [Fact]
        public async Task A_finished_run_releases_the_source_it_cancelled_through()
        {
            // One run per keystroke, and each allocates a cancellation source with the delay's timer
            // registered on it, so each has to be released when its run finishes.
            using var debounce = new Debounce(TimeSpan.Zero);
            CancellationToken seen = default;

            await debounce.Run(token => { seen = token; return Task.FromResult(1); }, _ => { });

            // A released source says so the moment its handle is asked for.
            Assert.Throws<ObjectDisposedException>(() => seen.WaitHandle);
        }

        [Fact]
        public async Task Cancelling_or_disposing_after_a_run_has_finished_is_harmless()
        {
            // The trap in releasing them: Cancel on a disposed source throws, and both TableView's exact
            // toggle and every component's teardown call Cancel long after the last run finished.
            var debounce = new Debounce(TimeSpan.Zero);

            await debounce.Run(_ => Task.FromResult(1), _ => { });

            debounce.Cancel();
            debounce.Dispose();
        }

        [Fact]
        public async Task A_run_overtaken_and_then_failing_fails_quietly()
        {
            // Its failure is as irrelevant as its result would have been: the caller acting on it would be
            // acting on a question nobody is asking any more, over the top of the newer answer.
            using var debounce = new Debounce(TimeSpan.Zero);

            await debounce.Run<int>(
                _ =>
                {
                    debounce.Cancel();
                    throw new InvalidOperationException("the engine moved underneath it");
                },
                _ => { });
        }

        [Fact]
        public async Task A_current_run_that_fails_still_fails()
        {
            // Only the superseded one is quiet. A failure of the call still being waited for is the caller's
            // to handle, and swallowing it would leave the caller showing an answer to nothing.
            using var debounce = new Debounce(TimeSpan.Zero);

            await Assert.ThrowsAsync<InvalidOperationException>(() => debounce.Run<int>(
                _ => throw new InvalidOperationException("broken"),
                _ => { }));
        }

        [Fact]
        public async Task Deferred_work_with_nothing_to_hand_over_still_commits()
        {
            using var debounce = new Debounce(TimeSpan.Zero);
            var committed = false;

            await debounce.Run(() =>
            {
                committed = true;
                return Task.CompletedTask;
            });

            Assert.True(committed);
        }
    }

    /// <summary>
    /// Parsing a value typed into the filter form. Tolerant on purpose: an unparseable value is a
    /// constraint the compiler drops, never an exception.
    /// </summary>
    public class FilterValueTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void An_empty_threshold_is_no_threshold(string value)
        {
            Assert.False(FilterValue.TryNumber(value, out double result));
            Assert.Equal(0, result);
        }

        [Fact]
        public void A_threshold_reads_in_the_invariant_culture_so_it_travels()
        {
            // The value ends up in Workspace.json and may be read back on a machine with a comma decimal
            // separator; a German-locale reader would otherwise turn 1.5 into 15.
            Assert.True(FilterValue.TryNumber(" 1.5 ", out double result));
            Assert.Equal(1.5, result);
        }
    }

    /// <summary>
    /// The chips row: the only place the whole query is stated in one readable line.
    /// </summary>
    public class FilterChipsTests
    {
        private static FilterSchema Schema() =>
            new FilterSchemaRegistry(new FakeSpellModelService(), new FakeTableCatalog(), new Preferences())
                .For(PaneDescriptor.Spell4);

        [Fact]
        public void Nothing_to_read_from_produces_no_chips()
        {
            Assert.Empty(FilterChips.For(null, Schema()));
            Assert.Empty(FilterChips.For(new FilterQuery(), null));

            Assert.Equal(0, FilterChips.ActiveCount(null, Schema()));
            Assert.Equal(0, FilterChips.ActiveCount(new FilterQuery(), null));
        }

        [Theory]
        [InlineData(FilterOperator.AtLeast, "≥")]
        [InlineData(FilterOperator.AtMost, "≤")]
        [InlineData(FilterOperator.Contains, "has")]
        [InlineData(FilterOperator.StartsWith, "=")]
        [InlineData(FilterOperator.Equals, "=")]
        public void A_chip_reads_its_comparison_as_a_symbol(FilterOperator op, string symbol)
        {
            var query = new FilterQuery();
            query.Set(FilterFields.CastTime, "500", op);

            FilterChip chip = Assert.Single(FilterChips.For(query, Schema()));

            Assert.Contains(symbol, chip.Label);
            Assert.Contains("500", chip.Label);
        }
    }
}
