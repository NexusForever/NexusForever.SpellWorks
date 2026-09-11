using System.Text;

namespace NexusForever.SpellWorks.Core.Test.Testing
{
    /// <summary>
    /// Serialises localised strings into the binary <c>.bin</c> layout <c>TextTable</c> reads, so tests can
    /// build a locale in memory.
    /// </summary>
    /// <remarks>
    /// <c>TextTable</c> does expose a constructor taking entries, but the loader under test reads a stream
    /// out of the archive - so the bytes are what a test has to supply.
    /// </remarks>
    public static class TextTableWriter
    {
        private const uint Signature = 0x4C544558;
        private const int HeaderSize = 112;
        private const int FieldSize = 8;

        /// <summary>
        /// Build a text table. Entries must be ordered by id - the reader sizes its lookup from the last one.
        /// </summary>
        public static MemoryStream Stream(uint language, params (uint Id, string Text)[] entries)
        {
            var strings = new MemoryStream();
            var offsets = new List<uint>();

            foreach ((uint _, string text) in entries)
            {
                // Offsets are counted in characters, not bytes.
                offsets.Add((uint)(strings.Length / 2));

                byte[] bytes = Encoding.Unicode.GetBytes(text ?? "");
                strings.Write(bytes, 0, bytes.Length);
                strings.WriteByte(0);
                strings.WriteByte(0);
            }

            var output = new MemoryStream();
            var writer = new BinaryWriter(output);

            int recordOffset = 0;
            int stringTableOffset = entries.Length * FieldSize;

            writer.Write(Signature);
            writer.Write(1u);                                  // Version
            writer.Write(language);
            writer.Write(0u);                                  // Unknown1
            writer.Write(0ul);                                 // TagNameLength
            writer.Write(0ul);                                 // TagNameOffset
            writer.Write(0ul);                                 // ShortNameLength
            writer.Write(0ul);                                 // ShortNameOffset
            writer.Write(0ul);                                 // LongNameLength
            writer.Write(0ul);                                 // LongNameOffset
            writer.Write((ulong)entries.Length);
            writer.Write((ulong)recordOffset);                 // records follow the header
            writer.Write((ulong)(strings.Length / 2));         // StringTableLength, in characters
            writer.Write((ulong)stringTableOffset);            // the blob follows the records

            for (var i = 0; i < entries.Length; i++)
            {
                writer.Write(entries[i].Id);
                writer.Write(offsets[i]);
            }

            writer.Write(strings.ToArray());
            writer.Flush();

            output.Position = 0;
            return output;
        }
    }
}
