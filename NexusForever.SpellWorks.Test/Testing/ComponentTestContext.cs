using System.IO;
using AngleSharp.Dom;
using Bunit;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NexusForever.SpellWorks.Components;
using NexusForever.SpellWorks.Core.Services;
using NexusForever.SpellWorks.Services;
using NexusForever.SpellWorks.Services.Filtering;
using Xunit;

namespace NexusForever.SpellWorks.Test.Testing
{
    /// <summary>
    /// The workspace a component test renders into: real workspace services over faked engine services,
    /// plus the <c>shell.js</c> module the gestures talk to.
    /// </summary>
    /// <remarks>
    /// <see cref="WorkspaceState"/>, <see cref="RowSource"/> and <see cref="WorkspaceStore"/> are the real
    /// types, not doubles - they are the behaviour under test. Only the engine boundary is faked, and the
    /// store is pointed at a scratch folder so a save never writes into the install.
    /// </remarks>
    public abstract class ComponentTestContext : BunitContext
    {
        protected FakeTableCatalog Catalog { get; } = new();
        protected FakeEngineHost Engine { get; } = new();
        protected FakeSpellModelService Models { get; } = new();
        protected FakeTextTableService Text { get; } = new();
        /// <summary>The windows the real <see cref="PopoutHost"/> hands out; assert pop-outs against this.</summary>
        protected FakePopoutWindowFactory Windows { get; } = new();
        protected FakeWindowBridge Bridge { get; } = new();
        protected FakeInstallationProbe Probe { get; } = new();
        protected FakeFolderPicker FolderPicker { get; } = new();
        protected IMessenger Messenger { get; } = new WeakReferenceMessenger();

        protected PopoutHost Popouts { get; }
        protected WorkspaceState State { get; }
        protected WorkspaceStore Store { get; }
        protected RowSource Rows { get; }

        /// <summary>
        /// The filter service the grids project through. A test holds one projection open on it to put two
        /// in flight at once.
        /// </summary>
        protected PassThroughFilterService Filtering { get; } = new();
        protected FilterSchemaRegistry Schemas { get; }
        protected PaletteIndex Palette { get; }

        /// <summary>The faked <c>./js/shell.js</c> module; assert gestures against its invocations.</summary>
        protected BunitJSModuleInterop ShellJs { get; }

        /// <summary>Scratch folder the workspace store persists into for this test.</summary>
        protected string StoreDirectory => _scratch;

        private readonly string _scratch;

        protected ComponentTestContext()
        {
            // The default is a second, which is comfortable on a plain run and far too tight under a
            // coverage profiler - every wait in the suite then fails at once. The waits still return as
            // soon as the condition holds, so a generous ceiling costs a passing run nothing.
            DefaultWaitTimeout = TimeSpan.FromSeconds(15);

            _scratch = Path.Combine(Path.GetTempPath(), "SpellWorks.Test", Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(_scratch);

            State   = new WorkspaceState(Messenger, Models, Catalog);
            Popouts = new PopoutHost(Windows, State);
            Schemas = new FilterSchemaRegistry(Models, Catalog, State.Preferences);
            Store   = new WorkspaceStore(State, Schemas, _scratch);
            Rows    = new RowSource(Models, Filtering, Catalog, Schemas);
            Palette = new PaletteIndex(Models, Catalog);

            // The views log the failures they recover from rather than showing them, so the container has
            // to be able to hand them a logger. Nothing is asserted against it: the point of the log line
            // is that the user is not shown one.
            Services.AddLogging();

            Services.AddSingleton(State);
            Services.AddSingleton(Store);
            Services.AddSingleton(Rows);
            Services.AddSingleton(Schemas);
            Services.AddSingleton(Palette);
            Services.AddSingleton(Messenger);
            Services.AddSingleton<ITableCatalog>(Catalog);
            Services.AddSingleton<IEngineHost>(Engine);
            Services.AddSingleton<ISpellModelService>(Models);
            Services.AddSingleton<ITextTableService>(Text);
            Services.AddSingleton<IPopoutHost>(Popouts);
            Services.AddSingleton<IInstallationProbe>(Probe);
            Services.AddSingleton<IFolderPicker>(FolderPicker);

            // Every gesture is handled in JS and reports back once, so the module is set up for all of them
            // and individual tests assert on what was invoked.
            ShellJs = JSInterop.SetupModule("./js/shell.js");
            ShellJs.SetupVoid("registerHotkeys", _ => true).SetVoidResult();
            ShellJs.SetupVoid("beginSplitDrag", _ => true).SetVoidResult();
            ShellJs.SetupVoid("beginTabDrag", _ => true).SetVoidResult();
            ShellJs.SetupVoid("beginPinDrag", _ => true).SetVoidResult();
            ShellJs.SetupVoid("beginColumnResize", _ => true).SetVoidResult();
            ShellJs.SetupVoid("focus", _ => true).SetVoidResult();
            ShellJs.SetupVoid("copyText", _ => true).SetVoidResult();
            ShellJs.Setup<Point>("menuPosition", _ => true).SetResult(new Point(0, 0));
            ShellJs.Setup<Size>("measure", _ => true).SetResult(new Size(120, 90));

            _module = JSInterop.JSRuntime
                .InvokeAsync<IJSObjectReference>("import", "./js/shell.js")
                .GetAwaiter()
                .GetResult();
        }

        private readonly IJSObjectReference _module;

        /// <summary>A context shaped the way the root components build one, for rendering a pane alone.</summary>
        protected ShellContext Context(bool isPopout = false, string popoutKey = null) => new()
        {
            State        = State,
            Rows         = Rows,
            Schemas      = Schemas,
            Catalog      = Catalog,
            Engine       = Engine,
            Models       = Models,
            Text         = Text,
            Popouts      = Popouts,
            Store        = Store,
            Bridge       = Bridge,
            Probe        = Probe,
            FolderPicker = FolderPicker,
            Refresh      = () => Task.CompletedTask,
            IsPopout     = isPopout,
            PopoutKey    = popoutKey,
            Js           = _module
        };

        /// <summary>
        /// Render a root component and wait until its first-render work has finished.
        /// </summary>
        /// <remarks>
        /// A root component's <c>OnAfterRenderAsync</c> imports <c>shell.js</c>, registers the hotkeys and
        /// then re-renders. That continuation is asynchronous, so a test that interacts the instant
        /// <c>Render</c> returns is racing it: the re-render replaces the elements it just looked up, and
        /// the events it fires land on handlers that are no longer attached. Waiting for the registration
        /// - the last thing that lifecycle does - removes the race rather than papering over it.
        /// </remarks>
        protected IRenderedComponent<T> RenderRoot<T>(Action<ComponentParameterCollectionBuilder<T>> parameters)
            where T : IComponent
        {
            IRenderedComponent<T> cut = Render(parameters);
            cut.WaitForState(() => InvocationCount("registerHotkeys") > 0);

            return cut;
        }

        /// <summary>Render <typeparamref name="T"/> beneath a cascaded <see cref="ShellContext"/>.</summary>
        protected IRenderedComponent<T> RenderUnderContext<T>(
            ShellContext context,
            Action<ComponentParameterCollectionBuilder<T>> parameters)
            where T : IComponent
        {
            return Render<CascadingValue<ShellContext>>(builder => builder
                    .Add(p => p.Value, context)
                    .Add(p => p.IsFixed, true)
                    .AddChildContent(parameters))
                .FindComponent<T>();
        }

        /// <summary>
        /// Find an element and fire an event on it inside one dispatcher turn.
        /// </summary>
        /// <remarks>
        /// A grid projects its rows on a background thread and re-renders when that lands, which can
        /// happen between a test's lookup and the event it fires - the element is replaced, the event goes
        /// to a handler that is no longer attached, and the click silently does nothing. Doing both on the
        /// dispatcher leaves no window for the re-render to slip into.
        /// </remarks>
        protected static void On<T>(IRenderedComponent<T> cut, string selector, Action<IElement> fire)
            where T : IComponent
        {
            cut.InvokeAsync(() => fire(cut.Find(selector))).GetAwaiter().GetResult();
        }

        /// <summary>As <see cref="On{T}"/>, for the nth element matching the selector.</summary>
        protected static void OnNth<T>(IRenderedComponent<T> cut, string selector, Index index, Action<IElement> fire)
            where T : IComponent
        {
            cut.InvokeAsync(() => fire(cut.FindAll(selector)[index])).GetAwaiter().GetResult();
        }

        // ------------------------------------------------------------------ filter form

        /// <summary>
        /// One control of the filter form, addressed by its schema field key rather than its position.
        /// </summary>
        /// <remarks>
        /// Positional addressing survived the single-value form only by luck: adding a field, or the
        /// per-condition buttons the boolean form needs, shifts every ordinal at once. The key is stable, and
        /// it is also the only way to say "the second Class condition in the second OR block".
        /// </remarks>
        protected static void Field<T>(IRenderedComponent<T> cut, string field, Action<IElement> fire,
            int block = 0, int index = 0, string control = "input")
            where T : IComponent
        {
            string selector = $"[data-group='{block}'] label.field[data-field='{field}'] {control}";
            OnNth(cut, selector, index, fire);
        }

        /// <summary>Type <paramref name="value"/> into a text or choice control of the filter form.</summary>
        protected static void SetField<T>(IRenderedComponent<T> cut, string field, string value,
            int block = 0, int index = 0, string control = "input")
            where T : IComponent
        {
            Field(cut, field, e => e.Change(value), block, index, control);
        }

        /// <summary>Click a toggle, negate, add or remove button belonging to one field.</summary>
        protected static void ClickField<T>(IRenderedComponent<T> cut, string field, string control,
            int block = 0, int index = 0)
            where T : IComponent
        {
            Field(cut, field, e => e.Click(), block, index, control);
        }

        // ------------------------------------------------------------------ flex rows

        /// <summary>
        /// One control of a flex card, addressed by the linked row its card offers and the row's position.
        /// </summary>
        /// <remarks>
        /// A flex card draws a row per constraint plus one blank one, so <c>^1</c> is always the blank row -
        /// which is what a test uses to add a constraint, and the one position that stays meaningful however
        /// many the card already holds.
        ///
        /// <paramref name="block"/> is a string rather than an ordinal because the common band is a group
        /// too, and it is named rather than numbered - pinning is one of the things a flex row has to do.
        /// </remarks>
        protected static void Flex<T>(IRenderedComponent<T> cut, string source, Action<IElement> fire,
            Index row, string block = "0", string control = "input")
            where T : IComponent
        {
            OnNth(cut, $"[data-group='{block}'] div.flex-row[data-flex='{source}'] {control}", row, fire);
        }

        /// <summary>Point a flex row at a column, which on the blank row is what creates the constraint.</summary>
        protected static void SetFlexColumn<T>(IRenderedComponent<T> cut, string source, string field,
            Index row, string block = "0")
            where T : IComponent
        {
            Flex(cut, source, e => e.Change(field), row, block, "select.col");
        }

        /// <summary>Type into a flex row's value box.</summary>
        protected static void SetFlexValue<T>(IRenderedComponent<T> cut, string source, string value,
            Index row, string block = "0")
            where T : IComponent
        {
            Flex(cut, source, e => e.Change(value), row, block);
        }

        /// <summary>Click one of a flex row's buttons.</summary>
        protected static void ClickFlex<T>(IRenderedComponent<T> cut, string source, string control,
            Index row, string block = "0")
            where T : IComponent
        {
            Flex(cut, source, e => e.Click(), row, block, control);
        }

        /// <summary>
        /// Poll until <paramref name="condition"/> holds.
        /// </summary>
        /// <remarks>
        /// Not <c>WaitForState</c>: that re-reads its predicate when the component renders, and the state
        /// this one waits on is a projection that has started and is deliberately not finishing - which is
        /// precisely a moment with no render to wait for.
        /// </remarks>
        protected static void Until(Func<bool> condition)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(15);

            while (!condition())
            {
                Assert.True(DateTime.UtcNow < deadline, "The condition did not hold before the timeout.");
                Thread.Sleep(10);
            }
        }

        /// <summary>Arguments of the single invocation of <paramref name="identifier"/> on the module.</summary>
        protected IReadOnlyList<object> InvocationArgs(string identifier)
        {
            return ShellJs.Invocations[identifier].Single().Arguments;
        }

        protected int InvocationCount(string identifier)
        {
            return ShellJs.Invocations[identifier].Count;
        }

        protected override async ValueTask DisposeAsyncCore()
        {
            await base.DisposeAsyncCore();

            try
            {
                Directory.Delete(_scratch, true);
            }
            catch (IOException)
            {
            }
        }
    }
}
