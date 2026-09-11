using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Spell;
using NexusForever.GameTable.Model;
using NexusForever.SpellWorks.Components;
using NexusForever.SpellWorks.Components.Views;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Models.Effect;
using NexusForever.SpellWorks.Core.Static;
using NexusForever.SpellWorks.Services;
using NexusForever.SpellWorks.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Test.Components
{
    /// <summary>
    /// One spell in full: the Spell, Effects and Procs sections, and the links that cross-reference them.
    /// </summary>
    public class SpellDetailViewTests : ComponentTestContext
    {
        private readonly PaneState _pane = new();

        private IRenderedComponent<DetailView> Detail(uint spellId) =>
            RenderUnderContext<DetailView>(Context(), p => p
                .Add(c => c.Scope, PaneDescriptor.Detail.Id)
                .Add(c => c.Pane, _pane)
                .Add(c => c.SpellId, spellId)
                .Add(c => c.Generation, 0));

        // ------------------------------------------------------------------ Spell section

        [Fact]
        public void Heads_the_view_with_the_spell_it_is_showing()
        {
            Spell(7, "Arcane Missile", tier: 3);
            _pane.SubTab = DetailSubTab.Spell;

            IRenderedComponent<DetailView> cut = Detail(7);

            Assert.Equal("Arcane Missile", cut.Find(".detail-title").TextContent);
            Assert.Contains("7 · Tier 3", cut.Find(".detail-sub").TextContent);
        }

        [Fact]
        public void Says_when_the_pane_is_locked_to_this_spell()
        {
            Spell(7);
            _pane.SubTab = DetailSubTab.Spell;
            _pane.LockedSpellId = 7;

            Assert.Contains("locked", Detail(7).Find(".detail-sub").TextContent);
        }

        [Fact]
        public void The_spell_section_describes_the_base_row_the_spell_row_and_the_tooltip()
        {
            Spell(7, "Arcane Missile");
            _pane.SubTab = DetailSubTab.Spell;

            IRenderedComponent<DetailView> cut = Detail(7);
            string[] titles = cut.FindAll(".card-head .title").Select(t => t.TextContent).ToArray();

            Assert.Equal(["Base", "Spell", "Tooltip"], titles);
        }

        [Fact]
        public void The_base_card_renders_the_columns_that_have_no_row_of_their_own()
        {
            ISpellModel spell = Spell(7);
            TestBase spellBase = (TestBase)spell.SpellBaseModel;
            spellBase.Entry.ClassIdPlayer = (byte)Class.Esper;
            spellBase.Entry.School = (uint)DamageType.Magic;
            _pane.SubTab = DetailSubTab.Spell;

            string markup = Detail(7).Markup;

            Assert.Contains(nameof(Class.Esper), markup);
            Assert.Contains(nameof(DamageType.Magic), markup);
            Assert.Contains("Target Angle", markup);
        }

        [Fact]
        public void A_missing_related_row_reads_as_none_rather_than_blank()
        {
            ISpellModel spell = Spell(7);
            ((TestBase)spell.SpellBaseModel).HitResultValue = null;
            ((TestBase)spell.SpellBaseModel).TargetMechanicsValue = null;
            _pane.SubTab = DetailSubTab.Spell;

            Assert.Contains("none", Detail(7).Markup);
        }

        [Fact]
        public void A_prerequisite_spell_is_a_link_to_that_spell()
        {
            // The link names a Spell4Base row, so the target is whichever spell is built on it.
            ISpellModel prerequisite = Spell(50, "Prerequisite");
            prerequisite.Entry.Spell4BaseIdBaseSpell = 50;

            ISpellModel spell = Spell(7, "Arcane Missile");
            ((TestBase)spell.SpellBaseModel).PrerequisiteSpellValue = prerequisite.SpellBaseModel.Entry;
            _pane.SubTab = DetailSubTab.Spell;

            IRenderedComponent<DetailView> cut = Detail(7);
            IElement link = cut.FindAll("button.link-btn").Single(b => b.TextContent == "go");

            link.Click();

            Assert.Equal(50u, State.SelectedSpellId);
        }

        // ------------------------------------------------------------------ Effects section

        [Fact]
        public void The_effects_section_gives_each_effect_its_own_card()
        {
            ISpellModel spell = Spell(7);
            AddEffect(spell, SpellEffectType.Damage);
            AddEffect(spell, SpellEffectType.Heal);
            _pane.SubTab = DetailSubTab.Effects;

            IRenderedComponent<DetailView> cut = Detail(7);

            Assert.Equal(2, cut.FindAll(".card").Count);
            Assert.Contains("DAMAGE", cut.Markup);
            Assert.Contains("HEAL", cut.Markup);
        }

        [Fact]
        public void A_spell_with_no_effects_says_so()
        {
            Spell(7);
            _pane.SubTab = DetailSubTab.Effects;

            Assert.Contains("this spell has no effect rows", Detail(7).Markup);
        }

        [Fact]
        public void An_effect_card_lays_out_all_ten_data_columns()
        {
            ISpellModel spell = Spell(7);
            AddEffect(spell, SpellEffectType.Damage);
            _pane.SubTab = DetailSubTab.Effects;

            IRenderedComponent<DetailView> cut = Detail(7);

            Assert.Equal(10, cut.FindAll(".data-cell").Count);
        }

        [Fact]
        public void A_data_column_is_labelled_by_the_projection_when_there_is_one()
        {
            ISpellModel spell = Spell(7);
            ISpellEffectModel effect = AddEffect(spell, SpellEffectType.VitalModifier);
            effect.RowData.Add(new VitalModifierSpellEffectRowData { Entry = effect.Entry });
            ((TestEffect)effect).ColumnDataValue = new VitalModifierSpellEffectColumnData();
            _pane.SubTab = DetailSubTab.Effects;

            IRenderedComponent<DetailView> cut = Detail(7);

            Assert.Equal("Vital", cut.FindAll(".data-cell-head")[0].TextContent);
        }

        [Fact]
        public void Without_a_projection_the_columns_fall_back_to_their_position_and_raw_bits()
        {
            ISpellModel spell = Spell(7);
            ISpellEffectModel effect = AddEffect(spell, SpellEffectType.Damage);
            effect.Entry.DataBits00 = 42;
            _pane.SubTab = DetailSubTab.Effects;

            IRenderedComponent<DetailView> cut = Detail(7);

            Assert.Equal("Data00", cut.FindAll(".data-cell-head")[0].TextContent);
            Assert.Contains("42", cut.FindAll(".data-cell-value")[0].TextContent);
        }

        [Fact]
        public void A_hyperlinked_data_column_follows_through_to_the_spell_it_names()
        {
            Spell(555, "Proxied Spell");
            Proxy(Spell(7), 555);

            IRenderedComponent<DetailView> cut = Detail(7);
            On(cut, ".data-cell button.link-btn", e => e.Click());

            Assert.Equal(555u, State.SelectedSpellId);
        }

        [Fact]
        public void A_proxy_offers_no_link_on_a_column_that_names_no_spell()
        {
            // Sprint (1316) is the case this exists for: its proxy leaves Data00 at zero. A button that
            // cannot go anywhere reads as broken, so it must not be on screen at all.
            Proxy(Spell(7), 0);

            IRenderedComponent<DetailView> cut = Detail(7);

            Assert.Empty(cut.FindAll(".data-cell button.link-btn"));
        }

        [Fact]
        public void A_proxy_offers_no_link_to_a_spell_that_is_not_loaded()
        {
            Proxy(Spell(7), 99999);

            IRenderedComponent<DetailView> cut = Detail(7);

            Assert.Empty(cut.FindAll(".data-cell button.link-btn"));
        }

        [Fact]
        public void A_proxy_links_the_later_columns_that_name_spells_too()
        {
            // The spell a proxy casts is not always in Data00 - Sprint carries it in Data01.
            Spell(40931, "Sprint Proxy");
            Proxy(Spell(7), 0, 40931);

            IRenderedComponent<DetailView> cut = Detail(7);

            Assert.Single(cut.FindAll(".data-cell button.link-btn"));
            On(cut, ".data-cell button.link-btn", e => e.Click());

            Assert.Equal(40931u, State.SelectedSpellId);
        }

        [Fact]
        public void A_hyperlink_names_the_spell_it_leads_to()
        {
            Spell(555, "Proxied Spell");
            Proxy(Spell(7), 555);

            IRenderedComponent<DetailView> cut = Detail(7);

            Assert.Equal("Open 555 — Proxied Spell",
                cut.Find(".data-cell button.link-btn").GetAttribute("title"));
        }

        [Fact]
        public void An_effect_type_that_cross_references_nothing_offers_no_links()
        {
            ISpellModel spell = Spell(7);
            ISpellEffectModel effect = AddEffect(spell, SpellEffectType.Damage);
            effect.Entry.DataBits00 = 7;
            effect.RowData.Add(new DamageSpellEffectRowData { Entry = effect.Entry });
            _pane.SubTab = DetailSubTab.Effects;

            IRenderedComponent<DetailView> cut = Detail(7);

            Assert.Empty(cut.FindAll(".data-cell button.link-btn"));
        }

        // ------------------------------------------------------------------ folding cards

        [Fact]
        public void A_card_folds_away_its_body_when_its_head_is_clicked()
        {
            Spell(7);
            _pane.SubTab = DetailSubTab.Spell;

            IRenderedComponent<DetailView> cut = Detail(7);
            int cards = cut.FindAll(".card").Count;

            On(cut, "button.card-head", e => e.Click());

            Assert.Equal(cards - 1, cut.FindAll(".card-body").Count);
            Assert.Contains("collapsed", cut.FindAll(".card")[0].ClassName);
        }

        [Fact]
        public void Clicking_a_folded_head_opens_it_again()
        {
            Spell(7);
            _pane.SubTab = DetailSubTab.Spell;

            IRenderedComponent<DetailView> cut = Detail(7);
            int bodies = cut.FindAll(".card-body").Count;

            On(cut, "button.card-head", e => e.Click());
            On(cut, "button.card-head", e => e.Click());

            Assert.Equal(bodies, cut.FindAll(".card-body").Count);
            Assert.DoesNotContain("collapsed", cut.FindAll(".card")[0].ClassName);
        }

        [Fact]
        public void An_effect_card_folds_away_its_data_strip_as_well()
        {
            Proxy(Spell(7), 0);

            IRenderedComponent<DetailView> cut = Detail(7);
            Assert.NotEmpty(cut.FindAll(".data-strip"));

            On(cut, "button.card-head", e => e.Click());

            Assert.Empty(cut.FindAll(".data-strip"));
        }

        [Fact]
        public void Only_the_card_that_was_clicked_folds()
        {
            Spell(7);
            _pane.SubTab = DetailSubTab.Spell;

            IRenderedComponent<DetailView> cut = Detail(7);
            int cards = cut.FindAll(".card").Count;

            OnNth(cut, "button.card-head", 1, e => e.Click());

            Assert.Equal(cards - 1, cut.FindAll(".card-body").Count);
            Assert.DoesNotContain("collapsed", cut.FindAll(".card")[0].ClassName);
            Assert.Contains("collapsed", cut.FindAll(".card")[1].ClassName);
        }

        [Fact]
        public void The_head_says_which_way_it_will_fold()
        {
            Spell(7);
            _pane.SubTab = DetailSubTab.Spell;

            IRenderedComponent<DetailView> cut = Detail(7);

            Assert.Equal("Collapse", cut.Find("button.card-head").GetAttribute("title"));
            Assert.Equal("true", cut.Find("button.card-head").GetAttribute("aria-expanded"));
            Assert.NotNull(cut.Find("button.card-head i.ph-caret-down"));

            On(cut, "button.card-head", e => e.Click());

            Assert.Equal("Expand", cut.Find("button.card-head").GetAttribute("title"));
            Assert.Equal("false", cut.Find("button.card-head").GetAttribute("aria-expanded"));
            Assert.NotNull(cut.Find("button.card-head i.ph-caret-right"));
        }

        [Fact]
        public void A_folded_card_stays_folded_when_the_view_moves_to_another_spell()
        {
            Spell(7, "First");
            Spell(8, "Second");
            _pane.SubTab = DetailSubTab.Spell;

            ShellContext context = Context();
            IRenderedComponent<CascadingValue<ShellContext>> root = Render(Tree(context, 7));

            On(root.FindComponent<DetailView>(), "button.card-head", e => e.Click());

            // The same component, now showing another spell: the cards are rebuilt from scratch, so a fold
            // remembered by position would be lost or land on the wrong card.
            root.Render(Tree(context, 8));

            Assert.Equal("Second", root.Find(".detail-title").TextContent);
            Assert.Contains("collapsed", root.FindAll(".card")[0].ClassName);
        }

        private Action<ComponentParameterCollectionBuilder<CascadingValue<ShellContext>>> Tree(
            ShellContext context, uint spellId)
        {
            return builder => builder
                .Add(p => p.Value, context)
                .Add(p => p.IsFixed, true)
                .AddChildContent<DetailView>(p => p
                    .Add(c => c.Scope, PaneDescriptor.Detail.Id)
                    .Add(c => c.Pane, _pane)
                    .Add(c => c.SpellId, spellId)
                    .Add(c => c.Generation, 0));
        }

        // ------------------------------------------------------------------ Procs section

        [Fact]
        public void The_procs_section_lists_what_this_spell_casts_and_what_casts_it()
        {
            Spell(555, "Proc Target");
            Spell(900, "Caster");
            ISpellModel spell = Spell(7);
            ((TestSpell)spell).Procs.Add(new TestProc { SpellId = 555, ProcType = (ProcType)2 });
            ((TestSpell)spell).ProcReferences.Add(900);
            _pane.SubTab = DetailSubTab.Procs;

            IRenderedComponent<DetailView> cut = Detail(7);

            Assert.Contains("Procs cast", cut.Markup);
            Assert.Contains("Referenced by", cut.Markup);
            Assert.Contains("Proc Target", cut.Markup);
            Assert.Contains("Caster", cut.Markup);
        }

        [Fact]
        public void A_spell_with_no_procs_says_so_in_both_directions()
        {
            Spell(7);
            _pane.SubTab = DetailSubTab.Procs;

            string markup = Detail(7).Markup;

            Assert.Contains("this spell casts no procs", markup);
            Assert.Contains("no spell procs into this one", markup);
        }

        [Fact]
        public void A_proc_that_points_at_an_unloaded_spell_says_so()
        {
            ISpellModel spell = Spell(7);
            ((TestSpell)spell).Procs.Add(new TestProc { SpellId = 12345 });
            _pane.SubTab = DetailSubTab.Procs;

            Assert.Contains("unknown spell", Detail(7).Markup);
        }

        [Fact]
        public void Following_a_proc_opens_the_spell_it_casts()
        {
            Spell(555, "Proc Target");
            ISpellModel spell = Spell(7);
            ((TestSpell)spell).Procs.Add(new TestProc { SpellId = 555 });
            _pane.SubTab = DetailSubTab.Procs;

            IRenderedComponent<DetailView> cut = Detail(7);
            cut.FindAll("button.link-btn")[0].Click();

            Assert.Equal(555u, State.SelectedSpellId);
        }

        // ------------------------------------------------------------------ fixtures

        private ISpellModel Spell(uint id, string description = "", uint tier = 1)
        {
            var spell = new TestSpell
            {
                Entry = new Spell4Entry { Id = id, TierIndex = tier },
                Description = description,
                SpellBaseModel = new TestBase { Entry = new Spell4BaseEntry { Id = id } }
            };

            Models.SpellModels[id] = spell;
            return spell;
        }

        /// <summary>A spell carrying one Proxy effect that names <paramref name="spellIds"/> in its Data columns.</summary>
        private ISpellEffectModel Proxy(ISpellModel spell, params uint[] spellIds)
        {
            ISpellEffectModel effect = AddEffect(spell, SpellEffectType.Proxy);

            if (spellIds.Length > 0) effect.Entry.DataBits00 = spellIds[0];
            if (spellIds.Length > 1) effect.Entry.DataBits01 = spellIds[1];
            if (spellIds.Length > 2) effect.Entry.DataBits02 = spellIds[2];

            effect.RowData.Add(new ProxySpellEffectRowData { Entry = effect.Entry });
            _pane.SubTab = DetailSubTab.Effects;

            return effect;
        }

        private static ISpellEffectModel AddEffect(ISpellModel spell, SpellEffectType type)
        {
            var effect = new TestEffect { Entry = new Spell4EffectsEntry(), Type = type };
            spell.Effects.Add(effect);

            return effect;
        }
    }
}
