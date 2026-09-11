using NexusForever.GameTable.Model;

namespace NexusForever.SpellWorks.Core.Models.Effect
{
    public class DefaultSpellEffectRowData : ISpellEffectRowData
    {
        public Spell4EffectsEntry Entry { get; set; }

        public virtual string Data00 => Entry.DataBits00.ToString();
        public virtual string Data01 => Entry.DataBits01.ToString();
        public virtual string Data02 => Entry.DataBits02.ToString();
        public virtual string Data03 => Entry.DataBits03.ToString();
        public virtual string Data04 => Entry.DataBits04.ToString();
        public virtual string Data05 => Entry.DataBits05.ToString();
        public virtual string Data06 => Entry.DataBits06.ToString();
        public virtual string Data07 => Entry.DataBits07.ToString();
        public virtual string Data08 => Entry.DataBits08.ToString();
        public virtual string Data09 => Entry.DataBits09.ToString();

        public virtual IReadOnlyList<int> HyperlinkedDataIndices => [];

        public uint DataBits(int index)
        {
            return index switch
            {
                0 => Entry.DataBits00,
                1 => Entry.DataBits01,
                2 => Entry.DataBits02,
                3 => Entry.DataBits03,
                4 => Entry.DataBits04,
                5 => Entry.DataBits05,
                6 => Entry.DataBits06,
                7 => Entry.DataBits07,
                8 => Entry.DataBits08,
                9 => Entry.DataBits09,
                _ => throw new ArgumentOutOfRangeException(nameof(index))
            };
        }
    }
}
