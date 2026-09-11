using Microsoft.JSInterop;
using NexusForever.Game.Static.Entity;
using NexusForever.Game.Static.Spell;
using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Services;
using NexusForever.SpellWorks.Services;
using NexusForever.SpellWorks.Services.Filtering;

namespace NexusForever.SpellWorks.Components
{
    /// <summary>
    /// The per-window services and shared behaviour every pane needs, cascaded from the root component so
    /// panes do not have to thread a dozen parameters.
    /// </summary>
    public sealed class ShellContext
    {
        public WorkspaceState State { get; init; }
        public RowSource Rows { get; init; }
        public FilterSchemaRegistry Schemas { get; init; }
        public ITableCatalog Catalog { get; init; }
        public IEngineHost Engine { get; init; }
        public ISpellModelService Models { get; init; }
        public ITextTableService Text { get; init; }
        public IPopoutHost Popouts { get; init; }
        public WorkspaceStore Store { get; init; }
        public IWindowBridge Bridge { get; init; }
        public IInstallationProbe Probe { get; init; }
        public IFolderPicker FolderPicker { get; init; }

        /// <summary>The <c>shell.js</c> module. Null until the root component's first render completes.</summary>
        public IJSObjectReference Js { get; set; }

        /// <summary>Re-render the window that owns this context. Always marshalled through InvokeAsync.</summary>
        public Func<Task> Refresh { get; init; }

        /// <summary>True in a pop-out window, where the pane owns its own close/dock chrome.</summary>
        public bool IsPopout { get; init; }

        /// <summary>Pane scope in a pop-out window; null in the main window, where scope is the view id.</summary>
        public string PopoutKey { get; init; }

        public ContextMenuModel Menu { get; private set; }

        /// <summary>
        /// Constraints active in <paramref name="scope"/>, for the tab badge.
        /// </summary>
        /// <remarks>
        /// A raw count, which is honest but incomplete: with OR blocks, five conditions across two blocks can
        /// return <em>more</em> rows than three in one. The chips row and the live row count are where the
        /// query actually reads back.
        /// </remarks>
        public int FilterCount(string scope, PaneDescriptor descriptor)
        {
            return FilterChips.ActiveCount(State.PaneStateFor(scope).Filters, Schemas?.For(descriptor));
        }

        // ------------------------------------------------------------------ menus

        public void ShowMenu(double x, double y, string title, string sub, List<MenuItem> items)
        {
            Menu = new ContextMenuModel { X = x, Y = y, Title = title, Sub = sub, Items = items };

            // The menu is rendered by the root component, not by whatever pane raised the event.
            _ = Refresh();
        }

        public void CloseMenu()
        {
            if (Menu == null)
                return;

            Menu = null;
            _ = Refresh();
        }

        /// <summary>Wrap a menu action so it always dismisses the menu first.</summary>
        private MenuItem Item(string icon, string label, string hint, Action invoke)
        {
            return new MenuItem(icon, label, hint, () =>
            {
                CloseMenu();
                invoke();
            });
        }

        public async Task Copy(string text)
        {
            if (Js != null)
                await Js.InvokeVoidAsync("copyText", text ?? "");
        }

        // ------------------------------------------------------------------ navigation helpers

        /// <summary>Open a view here, or in a new window when <paramref name="popout"/> is set.</summary>
        public void Open(string viewId, bool popout)
        {
            if (popout)
                Popouts.Popout(viewId);
            else
                State.SelectView(viewId);
        }

        public void OpenDetail(uint spellId, DetailSubTab? subTab, bool popout)
        {
            State.Select(spellId);

            if (!popout)
            {
                State.OpenDetail(spellId, subTab);
                return;
            }

            // The sub-tab belongs to the new window's own pane state, not to the detail pane in this one.
            string key = Popouts.Popout(PaneDescriptor.Detail.Id);
            if (key != null && subTab.HasValue)
                State.PaneStateFor(key).SubTab = subTab.Value;
        }

        /// <summary>
        /// Show the spells behind an effect type, here or in a new window. Mirrors <see cref="OpenDetail"/>:
        /// a pop-out gets its own pane scope, so the type has to be routed to the shared selection the new
        /// window will read.
        /// </summary>
        public void OpenEffectTypeSpells(SpellEffectType type, bool popout)
        {
            State.SelectEffectType(type);

            if (!popout)
            {
                State.OpenEffectTypeSpells(type);
                return;
            }

            Popouts.Popout(PaneDescriptor.EffectTypeSpells.Id);
        }

        // ------------------------------------------------------------------ menu builders

        /// <summary>
        /// Promote a flex column to a field of its own, or put it back.
        /// </summary>
        /// <remarks>
        /// A promotion moves nothing: the column keeps its key, so every condition already written on it
        /// stays exactly where it is and only the row's home changes - a labelled field at the top of
        /// every block rather than one line of the column picker's card. That is what makes both
        /// directions safe to offer as a single toggling item.
        ///
        /// Alone among the menu actions this one saves. The others move a selection or narrow a grid,
        /// which the next save picks up anyway; a promotion is a standing preference about the shape of
        /// the form, and losing it to a crash would be losing a decision rather than a view.
        /// </remarks>
        public List<MenuItem> PromoteMenu(PaneState pane, FilterColumnFieldSchema column)
        {
            bool promoted = pane.Promoted.Contains(column.Key);

            return
            [
                promoted
                    ? Item("ph ph-arrow-u-left-up", "Demote to the column picker", "", () =>
                    {
                        pane.Promoted.Remove(column.Key);
                        State.Notify();
                        Store.Save();
                    })
                    : Item("ph ph-push-pin", "Promote to its own field", "", () =>
                    {
                        pane.Promoted.Add(column.Key);
                        State.Notify();
                        Store.Save();
                    })
            ];
        }

        public List<MenuItem> SpellMenu(ISpellModel spell, string scope)
        {
            string className = EnumText.Name<Class>(spell.SpellBaseModel.Entry.ClassIdPlayer);

            return
            [
                Item("ph ph-lightning", "Open in Detail", "dbl-click", () => OpenDetail(spell.Id, DetailSubTab.Spell, false)),
                Item("ph ph-arrow-square-out", "Open in new window", "", () => OpenDetail(spell.Id, DetailSubTab.Spell, true)),
                Item("ph ph-flow-arrow", $"Show {spell.Effects.Count} effects", "", () => OpenDetail(spell.Id, DetailSubTab.Effects, false)),
                Item("ph ph-repeat", $"Show {spell.Procs.Count} procs", "", () => OpenDetail(spell.Id, DetailSubTab.Procs, false)),
                Item("ph ph-funnel", $"Filter to {className}", "", () =>
                {
                    PaneState state = State.PaneStateFor(scope);
                    state.Filters.Set(FilterFields.Class, className);
                    state.Mode = PaneMode.Rows;
                    State.Notify();
                }),
                Item("ph ph-copy", "Copy spell id", spell.Id.ToString(), () => _ = Copy(spell.Id.ToString()))
            ];
        }

        public List<MenuItem> EffectMenu(ISpellEffectModel effect, uint spellId, string scope)
        {
            return
            [
                Item("ph ph-lightning", "Open in Detail", "dbl-click", () => OpenDetail(spellId, DetailSubTab.Effects, false)),
                Item("ph ph-arrow-square-out", "Open in new window", "", () => OpenDetail(spellId, DetailSubTab.Effects, true)),
                Item("ph ph-funnel", $"Filter to {effect.Type}", "", () =>
                {
                    PaneState state = State.PaneStateFor(scope);
                    state.Filters.Set(FilterFields.EffectType, effect.Type.ToString());
                    state.Mode = PaneMode.Rows;
                    State.Notify();
                }),
                Item("ph ph-copy", "Copy data row", "", () => _ = Copy(DataRow(effect)))
            ];
        }

        private static string DataRow(ISpellEffectModel effect)
        {
            ISpellEffectRowData data = effect.RowData.FirstOrDefault();
            if (data == null)
                return "";

            return string.Join('\t', data.Data00, data.Data01, data.Data02, data.Data03, data.Data04,
                data.Data05, data.Data06, data.Data07, data.Data08, data.Data09);
        }

        public List<MenuItem> EffectTypeMenu(EffectTypeUsage usage)
        {
            uint typeId = (uint)usage.Type;

            return
            [
                Item("ph ph-crosshair", $"Show {usage.SpellIds.Count:n0} spells", "dbl-click",
                    () => OpenEffectTypeSpells(usage.Type, false)),
                Item("ph ph-arrow-square-out", "Show in new window", "",
                    () => OpenEffectTypeSpells(usage.Type, true)),
                Item("ph ph-funnel", $"Filter Spell4 to {usage.Type}", "", () =>
                {
                    PaneState state = State.PaneStateFor(PaneDescriptor.Spell4.Id);
                    state.Filters.Set(FilterFields.EffectType, usage.Type.ToString());
                    state.Mode = PaneMode.Rows;
                    State.SelectView(PaneDescriptor.Spell4.Id);
                }),
                Item("ph ph-copy", "Copy type id", typeId.ToString(), () => _ = Copy(typeId.ToString()))
            ];
        }

        public List<MenuItem> ProcMenu(ISpellProcModel proc)
        {
            return
            [
                Item("ph ph-lightning", $"Follow to spell {proc.SpellId}", "dbl-click", () => OpenDetail(proc.SpellId, DetailSubTab.Spell, false)),
                Item("ph ph-arrow-square-out", "Follow in new window", "", () => OpenDetail(proc.SpellId, DetailSubTab.Spell, true)),
                Item("ph ph-list-magnifying-glass", "Find in Spell4 browser", "", () =>
                {
                    PaneState state = State.PaneStateFor(PaneDescriptor.Spell4.Id);
                    state.Filters.Set(FilterFields.Id, proc.SpellId.ToString(), FilterOperator.StartsWith);
                    state.Mode = PaneMode.Rows;
                    State.SelectView(PaneDescriptor.Spell4.Id);
                }),
                Item("ph ph-copy", "Copy spell id", proc.SpellId.ToString(), () => _ = Copy(proc.SpellId.ToString()))
            ];
        }

        public List<MenuItem> TableMenu(TableDescriptor table)
        {
            string id = PaneDescriptor.GameTableId(table.Name);
            bool pinned = State.Pinned.Contains(id);

            return
            [
                Item("ph ph-table", $"Browse {table.Name}", "dbl-click", () => State.SelectView(id)),
                Item("ph ph-arrow-square-out", "Browse in new window", "", () => Popouts.Popout(id)),
                pinned
                    ? Item("ph ph-push-pin-slash", "Demote from sidebar", "unpin", () => State.Unpin(id))
                    : Item("ph ph-push-pin", "Promote to sidebar", "pin", () => State.Pin(id)),
                Item("ph ph-copy", "Copy table name", "", () => _ = Copy(table.Name + ".tbl"))
            ];
        }

        public List<MenuItem> GenericRowMenu(PaneDescriptor descriptor, GridRow row)
        {
            return
            [
                Item("ph ph-copy", "Copy row (tab separated)", "dbl-click", () => _ = Copy(string.Join('\t', row.Cells))),
                Item("ph ph-copy", "Copy Id", row.Cells.FirstOrDefault() ?? "", () => _ = Copy(row.Cells.FirstOrDefault())),
                Item("ph ph-arrow-square-out", $"Open {descriptor.TableName} in new window", "", () => Popouts.Popout(descriptor.Id))
            ];
        }

        public List<MenuItem> ViewMenu(string viewId, bool fromRail)
        {
            PaneDescriptor descriptor = State.Describe(viewId);
            bool pinned = State.Pinned.Contains(viewId);

            List<MenuItem> items =
            [
                Item(descriptor.Icon, "Open view", "", () => State.SelectView(viewId)),
                Item("ph ph-arrow-square-out", "Pop out as window", "", () => Popouts.Popout(viewId))
            ];

            if (descriptor.CanPin && !pinned)
                items.Add(Item("ph ph-push-pin", "Promote to sidebar", "pin", () => State.Pin(viewId)));

            if (descriptor.CanPin && pinned)
                items.Add(Item("ph ph-push-pin-slash", "Demote to tab only", "unpin", () =>
                {
                    State.Unpin(viewId);
                    State.SelectView(viewId);
                }));

            if (!fromRail)
                items.Add(Item("ph ph-x", "Close view", "", () => State.CloseView(viewId)));

            if (fromRail && !descriptor.CanPin)
                items.Add(Item("ph ph-funnel", "Open filter tab", "", () =>
                {
                    State.SelectView(viewId);
                    State.PaneStateFor(viewId).Mode = PaneMode.Filter;
                }));

            return items;
        }
    }
}
