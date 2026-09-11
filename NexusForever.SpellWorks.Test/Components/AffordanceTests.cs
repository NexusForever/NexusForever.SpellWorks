using System.Text.RegularExpressions;
using Xunit;

namespace NexusForever.SpellWorks.Test.Components
{
    /// <summary>
    /// Every control in the shipped markup can be operated. A test that drives each handler says nothing
    /// about a control with no handler at all - a caret that looks like a fold control but does nothing.
    ///
    /// This reads the markup and fails on a control that cannot be operated.
    /// </summary>
    public class AffordanceTests
    {
        /// <summary>Every element that opens a control, with the handler attributes that make it live.</summary>
        private static readonly Regex Control =
            new(@"<(?<tag>button|select)\b(?<attributes>[^>]*)>", RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex Handler =
            new(@"@on[a-z]+\s*=", RegexOptions.Compiled);

        [Fact]
        public void No_component_ships_a_button_or_select_that_cannot_be_operated()
        {
            List<string> dead = [];

            foreach (string file in ComponentFiles())
            {
                string markup = Markup(File.ReadAllText(file));

                foreach (Match match in Control.Matches(markup))
                {
                    string attributes = match.Groups["attributes"].Value;
                    if (Handler.IsMatch(attributes))
                        continue;

                    dead.Add($"{Path.GetFileName(file)}: <{match.Groups["tag"].Value}{Squash(attributes)}>");
                }
            }

            Assert.Empty(dead);
        }

        /// <summary>
        /// A caret, chevron or plus sign inside a plain container is the shape a dead affordance takes when
        /// it is not a button: it reads as "click me" and nothing happens. Icons that decorate an element
        /// which is itself a control are fine, so only containers are considered.
        /// </summary>
        [Fact]
        public void No_component_paints_a_fold_or_open_icon_onto_something_that_is_not_a_control()
        {
            var suspicious = new Regex(
                @"<(?<tag>div|span|td|th|li)\b(?<attributes>[^>]*)>\s*<i\b[^>]*?(?<icon>caret|chevron|plus|arrow-square)",
                RegexOptions.Compiled | RegexOptions.Singleline);

            List<string> dead = [];

            foreach (string file in ComponentFiles())
            {
                string markup = Markup(File.ReadAllText(file));

                foreach (Match match in suspicious.Matches(markup))
                {
                    if (Handler.IsMatch(match.Groups["attributes"].Value))
                        continue;

                    dead.Add($"{Path.GetFileName(file)}: <{match.Groups["tag"].Value}> holding a {match.Groups["icon"].Value} icon");
                }
            }

            Assert.Empty(dead);
        }

        /// <summary>The markup half of a component - everything ahead of its <c>@code</c> block.</summary>
        private static string Markup(string source)
        {
            int code = source.IndexOf("@code", StringComparison.Ordinal);
            return code < 0 ? source : source[..code];
        }

        private static IEnumerable<string> ComponentFiles()
        {
            string components = Path.Combine(RepositoryRoot(), "NexusForever.SpellWorks", "Components");
            Assert.True(Directory.Exists(components), $"No components at {components}");

            return Directory.EnumerateFiles(components, "*.razor", SearchOption.AllDirectories);
        }

        /// <summary>Walks out of the test binary's folder to the folder holding the solution.</summary>
        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "NexusForever.SpellWorks.sln")))
                directory = directory.Parent;

            Assert.NotNull(directory);
            return directory.FullName;
        }

        private static string Squash(string attributes)
        {
            string flat = Regex.Replace(attributes, @"\s+", " ").TrimEnd();
            return flat.Length > 90 ? flat[..90] + "…" : flat;
        }
    }
}
