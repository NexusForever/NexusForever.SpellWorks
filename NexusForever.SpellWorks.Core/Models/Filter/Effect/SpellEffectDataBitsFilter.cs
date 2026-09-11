namespace NexusForever.SpellWorks.Core.Models.Filter.Effect
{
    /// <summary>How a data-bits constraint compares.</summary>
    public enum DataBitsMatch
    {
        Equals,
        MaskAll,
        MaskAny
    }

    /// <summary>
    /// Matches one of an effect row's ten <c>DataBits</c> columns.
    /// </summary>
    /// <remarks>
    /// These columns carry whatever the effect type needs - a spell id, a percentage, a packed bitfield - so
    /// the constraint offers both an exact value and the two mask readings rather than guessing which the
    /// column is. An index outside the ten columns matches nothing, which is what a schema bug should look
    /// like rather than a silently widened grid.
    /// </remarks>
    public class SpellEffectDataBitsFilter : IModelFilter<ISpellEffectModel>
    {
        public const int Columns = 10;

        public int Index { get; set; }

        public uint Value { get; set; }

        public DataBitsMatch Match { get; set; }

        public bool Filter(ISpellEffectModel model)
        {
            if (model.Entry == null || Index < 0 || Index >= Columns)
                return false;

            uint actual = Read(model, Index);

            return Match switch
            {
                DataBitsMatch.MaskAll => MaskMode.All.Matches(actual, Value),
                DataBitsMatch.MaskAny => MaskMode.Any.Matches(actual, Value),
                _                     => actual == Value
            };
        }

        private static uint Read(ISpellEffectModel model, int index) => index switch
        {
            0 => model.Entry.DataBits00,
            1 => model.Entry.DataBits01,
            2 => model.Entry.DataBits02,
            3 => model.Entry.DataBits03,
            4 => model.Entry.DataBits04,
            5 => model.Entry.DataBits05,
            6 => model.Entry.DataBits06,
            7 => model.Entry.DataBits07,
            8 => model.Entry.DataBits08,
            _ => model.Entry.DataBits09
        };
    }
}
