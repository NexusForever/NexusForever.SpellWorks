using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace NexusForever.SpellWorks.Core.Services
{
    /// <summary>
    /// Builds a browsable catalog by reflecting over <see cref="IGameTableService"/>'s
    /// <c>GameTable&lt;T&gt;</c> properties once per load.
    /// </summary>
    /// <remarks>
    /// Column accessors are compiled to a single <see cref="Func{T, TResult}"/> per entry type, so rendering a
    /// cell never touches reflection.
    /// </remarks>
    public class TableCatalog : ITableCatalog
    {
        public IReadOnlyList<TableDescriptor> Tables => _tables;

        private readonly Dictionary<Type, (string[] Columns, Func<object, string[]> Values)> _schemas = [];
        // Concurrent, unlike _schemas: that one is filled only by Rebuild, on one thread, while this is
        // read on the filter schema registry's path - and a grid compiles its filter on the thread pool
        // while the component that owns it renders from the same schema. A plain dictionary corrupts
        // itself when those land together.
        private readonly ConcurrentDictionary<Type, IReadOnlyList<GameTableColumn>> _columns = new();

        // The same argument as _columns, answered differently: this one has a single writer - Rebuild, on
        // the UI thread - and many pool-thread readers, so publishing a finished dictionary by assignment
        // is enough and costs nothing per lookup. Clearing and refilling it in place would let a grid ask
        // for its table mid-rebuild and be told there is no such table.
        private Dictionary<string, TableDescriptor> _byName = new(StringComparer.OrdinalIgnoreCase);

        private List<TableDescriptor> _tables = [];

        #region Dependency Injection

        private readonly IGameTableService _gameTableService;

        public TableCatalog(
            IGameTableService gameTableService)
        {
            _gameTableService = gameTableService;
        }

        #endregion

        public TableDescriptor Get(string name)
        {
            // Snapshotted, so a rebuild that publishes a new index halfway through this lookup cannot be
            // observed at all - the same shape PaletteIndex.Search uses.
            Dictionary<string, TableDescriptor> byName = _byName;

            return name != null && byName.TryGetValue(name, out TableDescriptor descriptor) ? descriptor : null;
        }

        public IReadOnlyList<GameTableColumn> Columns(Type entryType)
        {
            if (entryType == null)
                return [];

            return _columns.GetOrAdd(entryType, Compile);
        }

        /// <summary>
        /// Walk one entry type's fields and compile an accessor pair per column.
        /// </summary>
        /// <remarks>
        /// Racing callers may both run this - <see cref="ConcurrentDictionary{TKey,TValue}.GetOrAdd"/>
        /// makes no promise otherwise - which is harmless: it reads nothing but the type, and one of the
        /// two results is then the only one anybody sees.
        /// </remarks>
        private static IReadOnlyList<GameTableColumn> Compile(Type entryType)
        {
            ParameterExpression parameter = Expression.Parameter(typeof(object), "entry");
            UnaryExpression typed = Expression.Convert(parameter, entryType);

            var columns = new List<GameTableColumn>();
            foreach (FieldInfo field in entryType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                MemberExpression value = Expression.Field(typed, field);
                bool numeric = IsNumeric(field.FieldType);

                columns.Add(new GameTableColumn(
                    field.Name,
                    field.FieldType,
                    numeric,
                    numeric
                        ? Expression.Lambda<Func<object, double>>(
                            Expression.Convert(value, typeof(double)), parameter).Compile()
                        : null,
                    Expression.Lambda<Func<object, string>>(
                        Format(value, field.FieldType), parameter).Compile()));
            }

            return columns;
        }

        /// <summary>
        /// Whether a column is a number for filtering purposes - which is to say, whether asking for
        /// "at least" it means anything.
        /// </summary>
        /// <remarks>
        /// <c>bool</c> and <c>char</c> are primitives that are not numbers here: comparing them by
        /// threshold would compile and mean nothing. An enum is left to the text reading, so it can be
        /// matched by the name it renders as rather than by an ordinal the user cannot see.
        /// </remarks>
        private static bool IsNumeric(Type type)
        {
            if (type.IsEnum)
                return false;

            return Type.GetTypeCode(type) switch
            {
                TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16
                    or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64
                    or TypeCode.Single or TypeCode.Double or TypeCode.Decimal => true,
                _ => false
            };
        }

        public void Clear()
        {
            _byName = new Dictionary<string, TableDescriptor>(StringComparer.OrdinalIgnoreCase);
            _tables = [];
        }

        public void Rebuild()
        {
            var tables = new List<TableDescriptor>();

            foreach (PropertyInfo property in typeof(IGameTableService)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType.IsGenericType
                    && p.PropertyType.GetGenericTypeDefinition().Name.StartsWith("GameTable")))
            {
                object gameTable = property.GetValue(_gameTableService);
                if (gameTable == null)
                    continue;

                Type entryType = property.PropertyType.GetGenericArguments()[0];
                (string[] columns, Func<object, string[]> values) = GetSchema(entryType);

                // GameTable<T>.Entries is a T[]; grab it once per rebuild rather than per access.
                var entries = (IReadOnlyList<object>)((IEnumerable)gameTable
                    .GetType()
                    .GetProperty("Entries")
                    .GetValue(gameTable))
                    .Cast<object>()
                    .ToArray();

                tables.Add(new TableDescriptor(
                    property.Name,
                    entryType,
                    entries.Count,
                    columns,
                    () => entries,
                    values));
            }

            List<TableDescriptor> ordered = tables.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();

            var byName = new Dictionary<string, TableDescriptor>(StringComparer.OrdinalIgnoreCase);
            foreach (TableDescriptor descriptor in ordered)
                byName[descriptor.Name] = descriptor;

            // Both published only once both are complete: a reader holds the previous catalog until the
            // new one is whole, rather than seeing a list of new tables it cannot yet resolve by name.
            _byName = byName;
            _tables = ordered;
        }

        private (string[] Columns, Func<object, string[]> Values) GetSchema(Type entryType)
        {
            if (_schemas.TryGetValue(entryType, out (string[], Func<object, string[]>) cached))
                return cached;

            FieldInfo[] fields = entryType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            string[] columns = fields.Select(f => f.Name).ToArray();

            ParameterExpression parameter = Expression.Parameter(typeof(object), "entry");
            UnaryExpression typed = Expression.Convert(parameter, entryType);

            var cells = new Expression[fields.Length];
            for (int i = 0; i < fields.Length; i++)
                cells[i] = Format(Expression.Field(typed, fields[i]), fields[i].FieldType);

            var accessor = Expression
                .Lambda<Func<object, string[]>>(Expression.NewArrayInit(typeof(string), cells), parameter)
                .Compile();

            (string[], Func<object, string[]>) schema = (columns, accessor);
            _schemas.Add(entryType, schema);
            return schema;
        }

        /// <summary>
        /// Render one field as display text. Arrays join with a space, everything else uses <c>ToString</c>.
        /// </summary>
        private static Expression Format(Expression value, Type type)
        {
            if (type.IsArray)
            {
                MethodInfo join = typeof(TableCatalog).GetMethod(nameof(JoinArray), BindingFlags.NonPublic | BindingFlags.Static);
                return Expression.Call(join, Expression.Convert(value, typeof(Array)));
            }

            if (type == typeof(string))
                return Expression.Coalesce(value, Expression.Constant(string.Empty));

            MethodInfo toString = type.GetMethod("ToString", Type.EmptyTypes);
            return Expression.Call(value, toString);
        }

        private static string JoinArray(Array array)
        {
            if (array == null)
                return string.Empty;

            return string.Join(' ', array.Cast<object>());
        }
    }
}
