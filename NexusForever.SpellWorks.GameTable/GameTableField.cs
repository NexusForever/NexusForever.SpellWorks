﻿using System.Runtime.InteropServices;
using NexusForever.SpellWorks.GameTable;

namespace NexusForever.SpellWorks.GameTable
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct GameTableField
    {
        public ulong NameLength;
        public ulong NameOffset;
        public DataType Type;
        public ushort Unknown2;
        public uint Unknown3;
    }
}
