using Bunit;
using NexusForever.SpellWorks.Components;
using Xunit;

namespace NexusForever.SpellWorks.Test.Testing
{
    public class HarnessTests : ComponentTestContext
    {
        [Fact]
        public void Renders_the_shell()
        {
            IRenderedComponent<Shell> cut = Render<Shell>(p => p.Add(c => c.Bridge, Bridge));

            Assert.Contains("SPELLWORKS", cut.Markup);
        }
    }
}
