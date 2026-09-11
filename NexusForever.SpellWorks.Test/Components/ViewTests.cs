using System.Globalization;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using NexusForever.SpellWorks.Components;
using NexusForever.SpellWorks.Components.Views;
using CommunityToolkit.Mvvm.Messaging;
using NexusForever.SpellWorks.Core.Messages;
using NexusForever.SpellWorks.Core.Services;
using NexusForever.GameTable.Model;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Models.Effect;
using NexusForever.SpellWorks.Core.Models.Filter;
using NexusForever.Game.Static.Spell;
using NexusForever.SpellWorks.Services;
using NexusForever.SpellWorks.Services.Filtering;
using NexusForever.SpellWorks.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Test.Components
{
    /// <summary>
    /// The filter form: chips for what is active, a field per constraint, and Apply.
    /// </summary>
    public class FilterViewTests : ComponentTestContext
    {
        private readonly PaneState _pane = new();
        private int _applied;

        private IRenderedComponent<FilterView> Form(PaneDescriptor descriptor = null) =>
            RenderUnderContext<FilterView>(Context(), p => p
                .Add(c => c.Descriptor, descriptor ?? PaneDescriptor.Spell4)
                .Add(c => c.Pane, _pane)
                .Add(c => c.SpellId, 0u)
                .Add(c => c.Applied, () => _applied++));

        [Fact]
        public void An_untouched_form_says_it_is_returning_everything()
        {
            Assert.Contains("no constraints — all rows returned", Form().Markup);
        }

        [Fact]
        public void An_active_constraint_shows_as_a_chip()
        {
            _pane.Filters.Set(FilterFields.HasProcs, "", FilterOperator.IsSet);

            IRenderedComponent<FilterView> cut = Form();

            Assert.Equal("has procs", cut.Find("button.chip").TextContent.Trim());
        }

        [Fact]
        public void Clearing_a_chip_drops_that_constraint()
        {
            _pane.Filters.Set(FilterFields.HasProcs, "", FilterOperator.IsSet);
            IRenderedComponent<FilterView> cut = Form();

            On(cut, "button.chip", e => e.Click());

            Assert.False(_pane.Filters.Has(FilterFields.HasProcs));
            Assert.Contains("no constraints", cut.Markup);
        }

        [Fact]
        public void Reset_all_clears_every_constraint_at_once()
        {
            _pane.Filters.Set(FilterFields.HasProcs, "", FilterOperator.IsSet);
            _pane.Filters.Set(FilterFields.Id, "123", FilterOperator.StartsWith);
            IRenderedComponent<FilterView> cut = Form();

            On(cut, "button.reset-btn", e => e.Click());

            Assert.Equal(0, _pane.Filters.ConditionCount);
        }

        [Fact]
        public void A_text_field_writes_through_to_the_form()
        {
            IRenderedComponent<FilterView> cut = Form();

            SetField(cut, FilterFields.Id, "123");

            Assert.Equal("123", _pane.Filters.ValueOf(FilterFields.Id));
        }

        [Fact]
        public void A_choice_field_writes_through_to_the_form()
        {
            IRenderedComponent<FilterView> cut = Form();

            SetField(cut, FilterFields.CastMethod, "Channeled", control: "select");

            Assert.Equal("Channeled", _pane.Filters.ValueOf(FilterFields.CastMethod));
        }

        [Fact]
        public void A_toggle_field_flips_on_and_off()
        {
            IRenderedComponent<FilterView> cut = Form();

            ClickField(cut, FilterFields.Deprecated, "button.toggle");

            // The field selects deprecated spells, so the toggle starts negated - "hide deprecated".
            FilterCondition condition = Assert.Single(_pane.Filters.On(FilterFields.Deprecated));
            Assert.True(condition.Negate);

            ClickField(cut, FilterFields.Deprecated, "button.toggle");
            Assert.False(_pane.Filters.Has(FilterFields.Deprecated));
        }

        [Fact]
        public void A_toggle_shows_whether_it_is_on()
        {
            _pane.Filters.Set(FilterFields.Deprecated, "", FilterOperator.IsSet, negate: true);

            IRenderedComponent<FilterView> cut = Form();

            Assert.Equal("on", cut.Find("button.toggle").TextContent.Trim());
            Assert.Contains("on", cut.Find("button.toggle").ClassName);
        }

        [Fact]
        public void Applying_returns_the_pane_to_the_grid_and_tells_it_to_reload()
        {
            IRenderedComponent<FilterView> cut = Form();

            On(cut, "button.apply-btn", e => e.Click());

            Assert.Equal(PaneMode.Rows, _pane.Mode);
            Assert.Equal(1, _applied);
        }

        [Fact]
        public void Each_view_offers_its_own_fields()
        {
            int spell4 = Form(PaneDescriptor.Spell4).FindAll("label.field").Count;
            int tables = Form(PaneDescriptor.Tables).FindAll("label.field").Count;

            Assert.True(spell4 > tables);
            Assert.True(tables > 0);
        }
    }

    /// <summary>
    /// The setup view: choosing a patch folder, reloading, and the preference toggles.
    /// </summary>
    public class SetupViewTests : ComponentTestContext
    {
        private int _reloaded;

        private IRenderedComponent<SetupView> Setup() =>
            RenderUnderContext<SetupView>(Context(), p => p.Add(c => c.Reloaded, () => _reloaded++));

        [Fact]
        public void Shows_the_patch_path_the_engine_is_mounted_against()
        {
            Engine.PatchPath = @"C:\WildStar\Patch";

            Assert.Contains(@"C:\WildStar\Patch", Setup().Find("input").GetAttribute("value"));
        }

        [Fact]
        public void Says_why_the_engine_could_not_read_the_archive()
        {
            // Without the reason, a wrong path looks like an empty client.
            Engine.State = EngineState.Failed;
            Engine.Error = "Could not find file 'D:\\Nowhere\\ClientData.archive'.";

            Assert.Contains("ClientData.archive", Setup().Find(".setup-problem").TextContent);
        }

        [Fact]
        public void Says_when_the_configuration_file_itself_could_not_be_read()
        {
            State.ConfigurationError = "Configuration.json could not be read and was ignored";

            Assert.Contains("Configuration.json", Setup().Find(".setup-problem").TextContent);
        }

        [Fact]
        public void Keeps_quiet_while_the_archive_is_mounted()
        {
            Engine.State = EngineState.Ready;

            Assert.Empty(Setup().FindAll(".setup-problem"));
        }

        [Fact]
        public void Lists_the_installations_when_the_shell_opens_the_browser_after_a_failure()
        {
            // The shell sets the flag; the view has to notice it without a click of its own.
            Probe.Candidates.Add(new InstallationCandidate(@"D:\WildStar\Patch", "fixed drive", "ph ph-hard-drives"));
            State.IsBrowsing = true;

            Assert.NotEmpty(Setup().FindAll("button.candidate"));
            Assert.Equal(1, Probe.DetectCount);
        }

        [Fact]
        public void Typing_a_path_updates_the_draft_without_reloading()
        {
            IRenderedComponent<SetupView> cut = Setup();

            On(cut, "input", e => e.Change(@"D:\Elsewhere"));

            Assert.Equal(@"D:\Elsewhere", State.PathDraft);
            Assert.Empty(Engine.Reloads);
        }

        [Fact]
        public void An_unmounted_draft_path_is_reported_as_a_change()
        {
            Engine.PatchPath = @"C:\WildStar\Patch";
            Probe.PatchFolders.Add(@"D:\Elsewhere");
            IRenderedComponent<SetupView> cut = Setup();

            On(cut, "input", e => e.Change(@"D:\Elsewhere"));

            Assert.Contains("unsaved change", cut.Markup);
        }

        [Fact]
        public void A_draft_path_with_no_archive_says_so()
        {
            Engine.PatchPath = @"C:\WildStar\Patch";
            IRenderedComponent<SetupView> cut = Setup();

            On(cut, "input", e => e.Change(@"D:\NotAnInstall"));

            Assert.Contains("no archive found here", cut.Markup);
        }

        [Fact]
        public void Browsing_searches_the_machine_for_installations()
        {
            Probe.Candidates.Add(new InstallationCandidate(@"D:\WildStar\Patch", "fixed drive", "ph ph-hard-drives"));
            IRenderedComponent<SetupView> cut = Setup();

            On(cut, "button.browse-btn", e => e.Click());

            Assert.Equal(1, Probe.DetectCount);
            Assert.Contains(@"D:\WildStar\Patch", cut.Markup);
        }

        [Fact]
        public void Choosing_a_found_installation_fills_in_the_path()
        {
            Probe.Candidates.Add(new InstallationCandidate(@"D:\WildStar\Patch", "fixed drive", "ph ph-hard-drives"));
            IRenderedComponent<SetupView> cut = Setup();
            On(cut, "button.browse-btn", e => e.Click());

            OnNth(cut, "button.candidate", 0, e => e.Click());

            Assert.Equal(@"D:\WildStar\Patch", State.PathDraft);
            Assert.False(State.IsBrowsing);
        }

        [Fact]
        public void The_folder_browser_fills_in_what_the_user_chose()
        {
            FolderPicker.Choice = @"E:\Chosen\Patch";
            IRenderedComponent<SetupView> cut = Setup();
            On(cut, "button.browse-btn", e => e.Click());

            OnNth(cut, "button.candidate", ^1, e => e.Click());

            Assert.Equal(@"E:\Chosen\Patch", State.PathDraft);
            Assert.Single(FolderPicker.Prompts);
        }

        [Fact]
        public void Cancelling_the_folder_browser_leaves_the_path_alone()
        {
            Engine.PatchPath = @"C:\WildStar\Patch";
            FolderPicker.Choice = null;
            IRenderedComponent<SetupView> cut = Setup();
            On(cut, "button.browse-btn", e => e.Click());

            OnNth(cut, "button.candidate", ^1, e => e.Click());

            // Nothing was picked, so the field still shows the mounted path.
            Assert.Equal(@"C:\WildStar\Patch", cut.Find("input").GetAttribute("value"));
        }

        [Fact]
        public void Applying_writes_the_path_and_asks_for_a_reload()
        {
            IRenderedComponent<SetupView> cut = Setup();
            On(cut, "input", e => e.Change(@"D:\Elsewhere"));

            On(cut, "button.apply-path-btn", e => e.Click());

            Assert.Equal(1, _reloaded);
            Assert.True(File.Exists(Path.Combine(StoreDirectory, "Configuration.json")));
        }

        [Fact]
        public void Applying_while_a_load_is_already_running_is_ignored()
        {
            // The button reads "loading…" mid-boot, but it is still a button; a second press must not
            // start a reload on top of the one in flight.
            IRenderedComponent<SetupView> cut = Setup();
            On(cut, "input", e => e.Change(@"D:\Elsewhere"));

            Engine.State = EngineState.Loading;
            State.Notify();

            On(cut, "button.apply-path-btn", e => e.Click());

            Assert.Equal(0, _reloaded);
            Assert.False(File.Exists(Path.Combine(StoreDirectory, "Configuration.json")));
        }

        [Fact]
        public void Reverting_returns_the_draft_to_the_mounted_path()
        {
            Engine.PatchPath = @"C:\WildStar\Patch";
            IRenderedComponent<SetupView> cut = Setup();
            On(cut, "input", e => e.Change(@"D:\Elsewhere"));

            On(cut, "button.revert-btn", e => e.Click());

            Assert.Equal(@"C:\WildStar\Patch", State.PathDraft);
        }

        [Fact]
        public void Describes_the_mounted_archive()
        {
            Engine.Info = new ArchiveInfo(@"C:\Patch", "ClientData.archive", 32, "en-US.bin", 4321,
                new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero));

            IRenderedComponent<SetupView> cut = Setup();

            Assert.Contains("ClientData.archive", cut.Markup);
            Assert.Contains("32 / 32", cut.Markup);
            Assert.Contains("4.321 strings", cut.Markup.Replace(",", "."));
        }

        [Fact]
        public void Says_when_nothing_is_mounted_yet()
        {
            Engine.Info = null;
            Engine.State = EngineState.Idle;

            Assert.Contains("not loaded", Setup().Markup);
        }

        [Fact]
        public void Says_when_the_last_load_failed()
        {
            Engine.Info = null;
            Engine.State = EngineState.Failed;

            Assert.Contains("failed to read", Setup().Markup);
        }

        [Fact]
        public void Changing_the_locale_switches_the_text_table_and_remembers_it()
        {
            IRenderedComponent<SetupView> cut = Setup();

            On(cut, "select", e => e.Change("deDE"));

            Assert.Equal("deDE", Text.Locale);
            Assert.Equal("deDE", State.Preferences.Locale);
        }

        [Fact]
        public void The_dropdown_shows_the_locale_being_read_when_the_preference_is_not_available()
        {
            // A remembered locale the mounted archives do not carry is not the one being read - the text
            // service falls back to the first archive. Showing the preference anyway offers a choice that
            // cannot be made: the entry a select already displays raises no change event when picked, so
            // the user would have no way to select the locale they are in fact looking at.
            State.Preferences.Locale = "frFR";
            Text.AvailableLocales    = ["enUS", "deDE"];

            Assert.Equal("enUS", Setup().Find("select").GetAttribute("value"));
        }

        [Fact]
        public void The_dropdown_shows_the_remembered_locale_when_the_archives_carry_it()
        {
            State.Preferences.Locale = "deDE";
            Text.AvailableLocales    = ["enUS", "deDE"];

            Assert.Equal("deDE", Setup().Find("select").GetAttribute("value"));
        }

        [Fact]
        public void Only_the_locales_actually_present_are_offered()
        {
            Text.AvailableLocales = ["enUS", "frFR"];

            IRenderedComponent<SetupView> cut = Setup();

            Assert.Equal(2, cut.FindAll("select option").Count);
        }

        [Fact]
        public void With_no_archive_mounted_the_current_locale_is_the_only_choice()
        {
            Text.AvailableLocales = [];

            Assert.Single(Setup().FindAll("select option"));
        }

        [Fact]
        public void A_preference_toggle_flips_and_is_persisted()
        {
            IRenderedComponent<SetupView> cut = Setup();
            bool before = State.Preferences.LoadOnStart;

            OnNth(cut, "button.toggle", 0, e => e.Click());

            Assert.NotEqual(before, State.Preferences.LoadOnStart);
            Assert.True(File.Exists(Path.Combine(StoreDirectory, "Workspace.json")));
        }

        [Fact]
        public void Every_preference_has_its_own_toggle()
        {
            Assert.Equal(6, Setup().FindAll("button.toggle").Count);
        }

        [Theory]
        [InlineData("Persist filters on start")]
        [InlineData("Persist promoted flex filters")]
        public void The_persistence_switches_are_offered_and_default_to_on(string label)
        {
            // Both default on, so a user who never touches them keeps their filters across restarts.
            IElement row = Setup().FindAll("label.field")
                .Single(f => f.QuerySelector("span.label").TextContent.Trim() == label);

            Assert.Equal("on", row.QuerySelector("button.toggle").TextContent.Trim());
        }

        [Fact]
        public void Turning_off_persisting_filters_flips_the_preference_and_saves()
        {
            IRenderedComponent<SetupView> cut = Setup();

            OnNth(cut, "button.toggle", 4, e => e.Click());

            Assert.False(State.Preferences.RestoreFilters);
            Assert.True(File.Exists(Path.Combine(StoreDirectory, "Workspace.json")));
        }

        [Fact]
        public void Turning_off_persisting_promotions_flips_only_that_preference()
        {
            IRenderedComponent<SetupView> cut = Setup();

            OnNth(cut, "button.toggle", 5, e => e.Click());

            Assert.False(State.Preferences.RestorePromoted);
            Assert.True(State.Preferences.RestoreFilters);
        }

        [Fact]
        public void The_float_tolerance_is_offered_with_the_epsilon_actually_in_use()
        {
            // A filter on a float column compares with slack, and a number nobody can see is a number
            // nobody can trust - so the setup states the epsilon it compares with.
            IElement box = Setup().Find("input.epsilon");

            Assert.Equal(NumberTolerance.Default.ToString(CultureInfo.InvariantCulture), box.GetAttribute("value"));
        }

        [Fact]
        public void Typing_a_float_tolerance_changes_what_the_filters_compare_with_and_is_persisted()
        {
            IRenderedComponent<SetupView> cut = Setup();

            On(cut, "input.epsilon", e => e.Change("0.25"));

            Assert.Equal(0.25d, State.Preferences.FilterEpsilon);
            Assert.True(File.Exists(Path.Combine(StoreDirectory, "Workspace.json")));
        }

        [Fact]
        public void A_tolerance_that_is_not_a_number_leaves_the_one_in_use_alone_and_says_so()
        {
            IRenderedComponent<SetupView> cut = Setup();

            On(cut, "input.epsilon", e => e.Change("not a number"));

            Assert.Equal(NumberTolerance.Default, State.Preferences.FilterEpsilon);

            // Kept and marked rather than discarded, as the filter form does with a value that will not
            // parse - a number that vanished on its way in is one the user cannot see to correct.
            Assert.Contains("invalid", cut.Find("input.epsilon").ClassName);
            Assert.Equal("not a number", cut.Find("input.epsilon").GetAttribute("value"));
        }

        [Fact]
        public void A_negative_tolerance_is_rejected_the_same_way()
        {
            // Negative slack is not a looser comparison, it is a narrower one that reads as neither.
            IRenderedComponent<SetupView> cut = Setup();

            On(cut, "input.epsilon", e => e.Change("-1"));

            Assert.Equal(NumberTolerance.Default, State.Preferences.FilterEpsilon);
            Assert.Contains("invalid", cut.Find("input.epsilon").ClassName);
        }

        [Theory]
        [InlineData("Infinity")]
        [InlineData("1e400")]
        public void A_tolerance_that_is_not_finite_is_rejected_and_the_workspace_still_saves(string typed)
        {
            // Both parse, to positive infinity - and a workspace holding one cannot be written as JSON at
            // all, so accepting it made this save and every later one throw, the exit save included.
            IRenderedComponent<SetupView> cut = Setup();

            On(cut, "input.epsilon", e => e.Change(typed));

            Assert.Equal(NumberTolerance.Default, State.Preferences.FilterEpsilon);
            Assert.Contains("invalid", cut.Find("input.epsilon").ClassName);

            Store.Save();
        }

        [Fact]
        public void A_readable_tolerance_clears_the_mark_again()
        {
            IRenderedComponent<SetupView> cut = Setup();

            On(cut, "input.epsilon", e => e.Change("nonsense"));
            On(cut, "input.epsilon", e => e.Change("0.5"));

            Assert.Equal(0.5d, State.Preferences.FilterEpsilon);
            Assert.DoesNotContain("invalid", cut.Find("input.epsilon").ClassName);
        }

        [Fact]
        public void The_clear_button_is_dead_until_something_is_promoted()
        {
            Assert.True(Setup().Find("button.clear-promoted").HasAttribute("disabled"));
        }

        [Fact]
        public void Clearing_promotions_empties_every_pane()
        {
            // Filters are cleared from the form they belong to, one pane at a time. Promotions are the
            // shape of that form rather than a constraint in it, so this is the only place they can all
            // be dropped at once.
            State.PaneStateFor("spells").Promoted.Add("fx:effects.DataBits00");
            State.PaneStateFor("effects").Promoted.Add("fx:effect.DataBits01");

            IRenderedComponent<SetupView> cut = Setup();
            // Its own class: the patch path card has a foot note of its own.
            Assert.Contains("2 promoted", cut.Find("span.promoted-count").TextContent);

            On(cut, "button.clear-promoted", e => e.Click());

            Assert.Empty(State.PaneStateFor("spells").Promoted);
            Assert.Empty(State.PaneStateFor("effects").Promoted);
            Assert.True(File.Exists(Path.Combine(StoreDirectory, "Workspace.json")));
        }

        [Fact]
        public void Clearing_promotions_leaves_the_conditions_alone()
        {
            // A promoted column's constraints are ordinary conditions on an ordinary key, so they come
            // back under the column picker rather than disappearing with the promotion.
            PaneState pane = State.PaneStateFor("spells");
            pane.Promoted.Add("fx:effects.DataBits00");
            pane.Filters.Set("fx:effects.DataBits00", "5");

            On(Setup(), "button.clear-promoted", e => e.Click());

            Assert.Equal("5", pane.Filters.ValueOf("fx:effects.DataBits00"));
        }
    }

    /// <summary>
    /// The detail view: the cards describing one spell, and the links between them.
    /// </summary>
    public class DetailViewTests : ComponentTestContext
    {
        private readonly PaneState _pane = new();

        private IRenderedComponent<DetailView> Detail(uint spellId) =>
            RenderUnderContext<DetailView>(Context(), p => p
                .Add(c => c.Scope, PaneDescriptor.Detail.Id)
                .Add(c => c.Pane, _pane)
                .Add(c => c.SpellId, spellId)
                .Add(c => c.Generation, 0));

        [Fact]
        public void With_no_spell_selected_it_says_so()
        {
            Assert.Contains("empty-state", Detail(0).Markup);
        }

        [Fact]
        public void A_spell_that_is_not_loaded_shows_the_same_empty_state()
        {
            Assert.Contains("empty-state", Detail(12345).Markup);
        }
    }

    /// <summary>
    /// A popped-out pane's own window chrome.
    /// </summary>
    public class PopoutRootTests : ComponentTestContext
    {
        /// <summary>A popped-out Detail window showing a spell whose one effect proxies another.</summary>
        private IRenderedComponent<PopoutRoot> DetailWindow()
        {
            Spell(555, "Proxied Spell");
            ISpellModel spell = Spell(7, "Source Spell");

            var effect = new TestEffect
            {
                Entry = new Spell4EffectsEntry { DataBits00 = 555 },
                Type  = SpellEffectType.Proxy
            };
            effect.RowData.Add(new ProxySpellEffectRowData { Entry = effect.Entry });
            spell.Effects.Add(effect);

            State.Select(7);
            State.PaneStateFor("key").SubTab = DetailSubTab.Effects;
            State.RegisterPopout("key", PaneDescriptor.Detail.Id);

            return RenderRoot<PopoutRoot>(p => p
                .Add(c => c.Bridge, Bridge)
                .Add(c => c.PaneKey, "key")
                .Add(c => c.ViewId, PaneDescriptor.Detail.Id));
        }

        private ISpellModel Spell(uint id, string description)
        {
            var spell = new TestSpell
            {
                Entry          = new Spell4Entry { Id = id, TierIndex = 1 },
                Description    = description,
                SpellBaseModel = new TestBase { Entry = new Spell4BaseEntry { Id = id } }
            };

            Models.SpellModels[id] = spell;
            return spell;
        }

        [Fact]
        public void A_hyperlink_moves_the_window_it_was_clicked_in()
        {
            IRenderedComponent<PopoutRoot> cut = DetailWindow();

            On(cut, ".data-cell button.link-btn", e => e.Click());

            Assert.Equal(555u, State.SelectedSpellId);
            cut.WaitForAssertion(() => Assert.Equal("Proxied Spell", cut.Find(".detail-title").TextContent));
        }

        [Fact]
        public void A_hyperlink_out_of_a_locked_window_opens_the_spell_rather_than_doing_nothing()
        {
            // The pane the "open" button is clicked in cannot leave the spell it is locked to, so the spell
            // has to open somewhere else or the button looks dead.
            IRenderedComponent<PopoutRoot> cut = DetailWindow();
            On(cut, "button.icon-btn[title='Lock this pane to the current spell']", e => e.Click());

            On(cut, ".data-cell button.link-btn", e => e.Click());

            Assert.Equal("Source Spell", cut.Find(".detail-title").TextContent);
            Assert.Equal(7u, State.SelectedIn("key"));
            Assert.Equal(555u, State.SelectedIn(PaneDescriptor.Detail.Id));
            Assert.Equal(PaneDescriptor.Detail.Id, State.Active);
        }

        private IRenderedComponent<PopoutRoot> Window(string viewId = null)
        {
            viewId ??= PaneDescriptor.Spell4.Id;
            State.RegisterPopout("key", viewId);

            return RenderRoot<PopoutRoot>(p => p
                .Add(c => c.Bridge, Bridge)
                .Add(c => c.PaneKey, "key")
                .Add(c => c.ViewId, viewId));
        }

        [Fact]
        public void The_window_title_names_the_view_it_carries()
        {
            Assert.Contains("Spell4 browser", Window().Find("div.popout-title").TextContent);
        }

        [Fact]
        public void Pressing_the_title_bar_drags_the_window()
        {
            IRenderedComponent<PopoutRoot> cut = Window();

            On(cut, "div.popout-title", e => e.MouseDown(new MouseEventArgs { Detail = 1 }));

            Assert.Equal(1, Bridge.DragCount);
        }

        [Fact]
        public void Double_pressing_the_title_bar_maximises_it()
        {
            IRenderedComponent<PopoutRoot> cut = Window();

            On(cut, "div.popout-title", e => e.MouseDown(new MouseEventArgs { Detail = 2 }));

            Assert.Equal(1, Bridge.MaximizeCount);
        }

        [Fact]
        public void Minimize_and_close_forward_to_the_window()
        {
            IRenderedComponent<PopoutRoot> cut = Window();

            On(cut, "button.title-btn[title='Minimize']", e => e.Click());
            On(cut, "button.title-btn.close", e => e.Click());

            Assert.Equal(1, Bridge.MinimizeCount);
            Assert.Equal(1, Bridge.CloseCount);
        }

        [Fact]
        public void Docking_returns_the_view_to_the_main_window()
        {
            // Popped out through the host, so there is a real window to dock - a pane key registered
            // straight into the workspace is not one the host knows anything about.
            string key = Popouts.Popout(PaneDescriptor.Spell4.Id);

            IRenderedComponent<PopoutRoot> cut = RenderRoot<PopoutRoot>(p => p
                .Add(c => c.Bridge, Bridge)
                .Add(c => c.PaneKey, key)
                .Add(c => c.ViewId, PaneDescriptor.Spell4.Id));

            On(cut, "button.title-btn[title='Dock back into main window']", e => e.Click());

            Assert.True(Windows.Windows[key].IsClosed);
            Assert.Equal(0, Popouts.OpenCount);
            Assert.Contains(PaneDescriptor.Spell4.Id, State.Open);
            Assert.Equal(PaneDescriptor.Spell4.Id, State.Active);
        }

        [Fact]
        public void The_toggle_switches_the_popped_out_pane_between_grid_and_filter()
        {
            IRenderedComponent<PopoutRoot> cut = Window();

            OnNth(cut, "div.segmented button", 1, e => e.Click());

            Assert.Equal(PaneMode.Filter, State.PaneStateFor("key").Mode);
        }

        [Fact]
        public void A_popped_out_setup_view_has_nothing_to_filter()
        {
            IRenderedComponent<PopoutRoot> cut = Window(PaneDescriptor.Setup.Id);

            Assert.Empty(cut.FindAll("div.segmented"));
        }

        [Fact]
        public void A_popped_out_pane_keeps_its_own_filter_set()
        {
            // The window shares the selected spell, but not the constraints.
            State.PaneStateFor("key").Filters.Set(FilterFields.HasProcs, "", FilterOperator.IsSet);

            IRenderedComponent<PopoutRoot> cut = Window();

            Assert.Contains("badge", cut.Find("div.segmented").InnerHtml);
            Assert.Equal(0, State.PaneStateFor(PaneDescriptor.Spell4.Id).Filters.ConditionCount);
        }

        [Fact]
        public void The_split_and_pin_callbacks_are_inert_because_a_pop_out_has_neither()
        {
            // The shared module has one shape, so the callbacks exist but do nothing here.
            IRenderedComponent<PopoutRoot> cut = Window();

            cut.Instance.OnSplitDragged([2.0, 1.0]);
            cut.Instance.OnPinDropped("tbl:X", "pin");

            Assert.Empty(State.Flexes);
            Assert.Empty(State.Pinned);
        }

        [Fact]
        public void The_toggle_switches_the_popped_out_pane_back_to_the_grid()
        {
            IRenderedComponent<PopoutRoot> cut = Window();

            OnNth(cut, "div.segmented button", 1, e => e.Click());
            OnNth(cut, "div.segmented button", 0, e => e.Click());

            Assert.Equal(PaneMode.Rows, State.PaneStateFor("key").Mode);
        }

        [Fact]
        public void Losing_focus_with_no_menu_open_does_nothing()
        {
            IRenderedComponent<PopoutRoot> cut = Window();

            cut.InvokeAsync(() => cut.Instance.OnWindowBlur()).GetAwaiter().GetResult();

            cut.WaitForAssertion(() => Assert.DoesNotContain("menu-items", cut.Markup));
        }

        [Fact]
        public void Losing_focus_dismisses_an_open_menu()
        {
            IRenderedComponent<PopoutRoot> cut = WithOpenMenu();

            cut.InvokeAsync(() => cut.Instance.OnWindowBlur()).GetAwaiter().GetResult();

            cut.WaitForAssertion(() => Assert.DoesNotContain("menu-items", cut.Markup));
        }

        [Fact]
        public void Choosing_a_menu_item_in_the_window_closes_the_menu_behind_it()
        {
            // The menu reports its own close back to the window, which has to re-render without it.
            IRenderedComponent<PopoutRoot> cut = WithOpenMenu();

            On(cut, "button.menu-item", e => e.Click());

            cut.WaitForAssertion(() => Assert.DoesNotContain("menu-items", cut.Markup));
        }

        [Fact]
        public void Pressing_the_window_with_no_menu_open_does_nothing()
        {
            IRenderedComponent<PopoutRoot> cut = Window();

            On(cut, "div.popout-root", e => e.MouseDown(new MouseEventArgs()));

            Assert.DoesNotContain("menu-items", cut.Markup);
        }

        [Fact]
        public void The_hotkeys_are_registered_with_the_module_on_first_render()
        {
            IRenderedComponent<PopoutRoot> cut = Window();

            cut.WaitForAssertion(() => Assert.Equal(1, InvocationCount("registerHotkeys")));
        }

        [Fact]
        public void A_reload_elsewhere_refreshes_what_the_window_is_showing()
        {
            Catalog.With("Spell4Effects", ["Id"], [["1"]]);
            IRenderedComponent<PopoutRoot> cut = Window(PaneDescriptor.GameTableId("Spell4Effects"));

            // Another window finished a load; this one has to pick up the new row counts.
            IMessengerExtensions.Send(Messenger, new SpellResourcesLoaded());

            cut.WaitForAssertion(() => Assert.Contains("1 rows", cut.Find("div.popout-title").TextContent));
        }

        [Fact]
        public void Pressing_the_window_dismisses_an_open_menu()
        {
            IRenderedComponent<PopoutRoot> cut = WithOpenMenu();

            On(cut, "div.popout-root", e => e.MouseDown(new MouseEventArgs()));

            cut.WaitForAssertion(() => Assert.DoesNotContain("menu-items", cut.Markup));
        }

        [Fact]
        public async Task Escape_dismisses_an_open_menu_in_the_window()
        {
            IRenderedComponent<PopoutRoot> cut = WithOpenMenu();

            await cut.InvokeAsync(() => cut.Instance.OnHotkey("escape", false));

            cut.WaitForAssertion(() => Assert.DoesNotContain("menu-items", cut.Markup));
        }

        [Fact]
        public async Task Arrow_keys_walk_the_open_menu()
        {
            IRenderedComponent<PopoutRoot> cut = WithOpenMenu();

            await cut.InvokeAsync(() => cut.Instance.OnHotkey("down", false));
            Assert.Contains("cursor", cut.FindAll("button.menu-item")[0].ClassName);

            await cut.InvokeAsync(() => cut.Instance.OnHotkey("up", false));
            Assert.Contains("cursor", cut.FindAll("button.menu-item")[^1].ClassName);
        }

        [Fact]
        public async Task Enter_runs_the_menu_item_under_the_cursor()
        {
            IRenderedComponent<PopoutRoot> cut = WithOpenMenu();

            await cut.InvokeAsync(() => cut.Instance.OnHotkey("down", false));
            await cut.InvokeAsync(() => cut.Instance.OnHotkey("enter", false));

            // The first item of a table row's menu browses that table.
            Assert.Equal(PaneDescriptor.GameTableId("Spell4Effects"), State.Active);
        }

        [Fact]
        public async Task A_key_the_window_does_not_handle_is_ignored()
        {
            IRenderedComponent<PopoutRoot> cut = WithOpenMenu();

            await cut.InvokeAsync(() => cut.Instance.OnHotkey("palette", false));

            Assert.Contains("menu-items", cut.Markup);
        }

        [Fact]
        public void The_window_stops_listening_once_it_is_gone()
        {
            IRenderedComponent<PopoutRoot> cut = Window();
            int before = State.Open.Count;

            Renderer.DisposeComponents();

            State.SelectView(PaneDescriptor.Tables.Id);
            Assert.Equal(before + 1, State.Open.Count);
        }

        /// <summary>A window showing the table list, with a row's menu open.</summary>
        private IRenderedComponent<PopoutRoot> WithOpenMenu()
        {
            Catalog.With("Spell4Effects", ["Id"], [["1"]]);

            IRenderedComponent<PopoutRoot> cut = Window(PaneDescriptor.Tables.Id);
            cut.WaitForState(() => cut.FindAll("tbody tr").Any(r => r.QuerySelector("td") != null));

            cut.InvokeAsync(() => cut.FindAll("tbody tr")
                .First(r => r.QuerySelector("td") != null)
                .ContextMenu(new MouseEventArgs())).GetAwaiter().GetResult();

            cut.WaitForAssertion(() => Assert.Contains("menu-items", cut.Markup));
            return cut;
        }
    }
}
