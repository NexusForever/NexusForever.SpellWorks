using System.Reflection;
using System.Text;
using NexusForever.GameTable;

namespace NexusForever.SpellWorks.Core.Test.Testing
{
    /// <summary>
    /// Serialises entries into the binary <c>.tbl</c> layout <see cref="GameTable{T}"/> reads, so tests can
    /// build real game tables in memory.
    /// </summary>
    /// <remarks>
    /// <see cref="GameTable{T}.Entries"/> has a private, non-virtual setter and the only constructors take a
    /// path or a stream, so a table can be neither faked nor subclassed. Writing the format is what lets a
    /// test build one without reaching into the type - the table under test is a real one, built through its
    /// own public constructor.
    /// </remarks>
    public static class GameTableWriter
    {
        private const uint Signature = 0x4454424C;
        private const int HeaderSize = 96;
        private const int FieldSize = 24;

        // DataType as the reader sees it. Sizes below are what the reader consumes per field.
        private const ushort TypeUInt = 3;
        private const ushort TypeSingle = 4;
        private const ushort TypeBoolean = 11;
        private const ushort TypeULong = 20;
        private const ushort TypeString = 130;

        public static GameTable<T> Table<T>(params T[] entries) where T : class, new()
        {
            return new GameTable<T>(Stream(entries));
        }

        public static MemoryStream Stream<T>(params T[] entries) where T : class, new()
        {
            List<Column> columns = Columns(typeof(T));

            int recordSize = columns.Sum(c => c.Size);
            int recordBlock = recordSize * entries.Length;

            // Strings live in a blob after the records; a record holds a byte offset into it.
            var strings = new MemoryStream();
            var interned = new Dictionary<string, uint>();

            uint Intern(string value)
            {
                value ??= "";
                if (interned.TryGetValue(value, out uint known))
                    return known;

                var offset = (uint)strings.Length;
                byte[] bytes = Encoding.Unicode.GetBytes(value);
                strings.Write(bytes, 0, bytes.Length);
                strings.WriteByte(0);
                strings.WriteByte(0);

                interned[value] = offset;
                return offset;
            }

            var records = new MemoryStream();
            var recordWriter = new BinaryWriter(records);

            foreach (T entry in entries)
                foreach (Column column in columns)
                    Write(recordWriter, column, entry, recordBlock, Intern);

            recordWriter.Flush();

            var output = new MemoryStream();
            var writer = new BinaryWriter(output);

            writer.Write(Signature);
            writer.Write(1u);                                    // Version
            writer.Write(0ul);                                   // NameLength
            writer.Write(0ul);                                   // Unknown1
            writer.Write((ulong)recordSize);
            writer.Write((ulong)columns.Count);
            writer.Write(0ul);                                   // FieldOffset - fields follow the header
            writer.Write((ulong)entries.Length);
            writer.Write((ulong)(recordBlock + strings.Length)); // TotalRecordSize - records plus the blob
            writer.Write((ulong)(columns.Count * FieldSize));    // RecordOffset - records follow the fields
            writer.Write(MaxId(columns, entries));
            writer.Write(0ul);                                   // LookupOffset
            writer.Write(0ul);                                   // Unknown2

            foreach (Column column in columns)
            {
                writer.Write(0ul);                               // NameLength
                writer.Write(0ul);                               // NameOffset
                writer.Write(column.Type);
                writer.Write((ushort)0);                         // Unknown2
                writer.Write(0u);                                // Unknown3
            }

            writer.Write(records.ToArray());
            writer.Write(strings.ToArray());
            writer.Flush();

            output.Position = 0;
            return output;
        }

        private static void Write<T>(BinaryWriter writer, Column column, T entry, int recordBlock, Func<string, uint> intern)
        {
            object value = column.Read(entry);

            switch (column.Type)
            {
                case TypeString:
                {
                    // Both offsets are written non-zero and equal: the reader takes the larger, and skips a
                    // further four bytes only when the first one is zero.
                    uint offset = (uint)recordBlock + intern((string)value);
                    writer.Write(offset);
                    writer.Write(offset);
                    break;
                }
                case TypeULong:
                    writer.Write(value == null ? 0ul : Convert.ToUInt64(value));
                    break;
                case TypeSingle:
                    writer.Write(value == null ? 0f : Convert.ToSingle(value));
                    break;
                case TypeBoolean:
                    writer.Write(value != null && Convert.ToBoolean(value) ? 1u : 0u);
                    break;
                default:
                    writer.Write(ToUInt(value));
                    break;
            }
        }

        private static uint ToUInt(object value)
        {
            if (value == null)
                return 0u;

            Type type = value.GetType();
            return type.IsEnum
                ? Convert.ToUInt32(Convert.ChangeType(value, Enum.GetUnderlyingType(type)))
                : Convert.ToUInt32(value);
        }

        /// <summary>The reader indexes a lookup array by the first column, so every id has to fit inside it.</summary>
        private static ulong MaxId<T>(List<Column> columns, T[] entries)
        {
            if (entries.Length == 0)
                return 1ul;

            uint highest = entries.Max(e => ToUInt(columns[0].Read(e)));
            return highest + 1ul;
        }

        private static List<Column> Columns(Type model)
        {
            var columns = new List<Column>();

            foreach (FieldInfo field in model.GetFields())
            {
                var array = field.GetCustomAttribute<GameTableFieldArrayAttribute>();

                if (array == null)
                {
                    columns.Add(new Column(TypeOf(field.FieldType), field, -1));
                    continue;
                }

                // An array member is stored flat, one field per element.
                ushort element = TypeOf(field.FieldType.GetElementType());
                for (int i = 0; i < array.Length; i++)
                    columns.Add(new Column(element, field, i));
            }

            return columns;
        }

        private static ushort TypeOf(Type type)
        {
            if (type.IsEnum)
                type = Enum.GetUnderlyingType(type);

            if (type == typeof(string))
                return TypeString;
            if (type == typeof(float))
                return TypeSingle;
            if (type == typeof(bool))
                return TypeBoolean;
            if (type == typeof(ulong) || type == typeof(long))
                return TypeULong;

            return TypeUInt;
        }

        private sealed record Column(ushort Type, FieldInfo Field, int Index)
        {
            public int Size => Type switch
            {
                TypeULong  => 8,
                TypeString => 8,
                _          => 4
            };

            public object Read(object entry)
            {
                object value = Field.GetValue(entry);

                if (Index < 0)
                    return value;

                var array = (Array)value;
                return array != null && Index < array.Length ? array.GetValue(Index) : null;
            }
        }
    }
}
