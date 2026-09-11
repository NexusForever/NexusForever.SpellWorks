using System.Reflection;
using NexusForever.SpellWorks.Core.Services;

namespace NexusForever.SpellWorks.Core.Test.Testing
{
    /// <summary>
    /// A complete in-memory client archive: every table <see cref="IGameTableService"/> exposes, written in
    /// the real <c>.tbl</c> format, so the loaders can run end to end with no client installation.
    /// </summary>
    /// <remarks>
    /// The table set is taken from the interface itself rather than restated here, so a table added to
    /// <see cref="IGameTableService"/> cannot silently go missing from the fixture.
    /// </remarks>
    public sealed class SyntheticArchive
    {
        private readonly Dictionary<string, object[]> _entries = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Names of every table the archive will contain, matching the service's properties.</summary>
        public static IReadOnlyList<(string Name, Type EntryType)> Tables { get; } =
            typeof(IGameTableService)
                .GetProperties()
                .Where(p => p.PropertyType.IsGenericType)
                .Select(p => (p.Name, p.PropertyType.GetGenericArguments()[0]))
                .ToList();

        /// <summary>Give <paramref name="table"/> the supplied rows instead of leaving it empty.</summary>
        public SyntheticArchive With(string table, params object[] entries)
        {
            _entries[table] = entries;
            return this;
        }

        public FakeArchiveReader Build(params string[] omit)
        {
            var reader = new FakeArchiveReader();
            var omitted = new HashSet<string>(omit, StringComparer.OrdinalIgnoreCase);

            foreach ((string name, Type entryType) in Tables)
            {
                if (omitted.Contains(name))
                    continue;

                object[] entries = _entries.TryGetValue(name, out object[] rows) ? rows : [];
                reader.With(Path.Combine("DB", name + ".tbl"), Serialise(entryType, entries));
            }

            return reader;
        }

        public IArchiveService AsArchiveService(params string[] omit) => new FakeArchiveService(Build(omit));

        private static MemoryStream Serialise(Type entryType, object[] entries)
        {
            Array typed = Array.CreateInstance(entryType, entries.Length);
            for (int i = 0; i < entries.Length; i++)
                typed.SetValue(entries[i], i);

            MethodInfo writer = typeof(GameTableWriter)
                .GetMethod(nameof(GameTableWriter.Stream))!
                .MakeGenericMethod(entryType);

            return (MemoryStream)writer.Invoke(null, [typed]);
        }
    }

    /// <summary>An archive service already mounted against in-memory archives.</summary>
    public sealed class FakeArchiveService : IArchiveService
    {
        public FakeArchiveService(IArchiveReader main, params IArchiveReader[] localisations)
        {
            MainArchive = main;
            LocalisationArchives = localisations;
        }

        public IArchiveReader MainArchive { get; set; }
        public IReadOnlyList<IArchiveReader> LocalisationArchives { get; set; }
        public string PatchPath { get; set; } = @"C:\WildStar\Patch";
        public string ArchiveName { get; set; } = "ClientData.archive";

        public int InitialiseCount { get; private set; }

        public Task Initialise()
        {
            InitialiseCount++;
            return Task.CompletedTask;
        }
    }
}
