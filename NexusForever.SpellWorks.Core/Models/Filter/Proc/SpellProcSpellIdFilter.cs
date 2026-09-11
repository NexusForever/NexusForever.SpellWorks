namespace NexusForever.SpellWorks.Core.Models.Filter.Proc
{
    /// <summary>Matches procs whose cast spell id starts with <see cref="IdPrefix"/>.</summary>
    public class SpellProcSpellIdFilter : IModelFilter<ISpellProcModel>
    {
        public string IdPrefix { get; set; }

        public bool Filter(ISpellProcModel model)
        {
            if (string.IsNullOrWhiteSpace(IdPrefix))
                return true;

            return model.SpellId.ToString().StartsWith(IdPrefix, StringComparison.Ordinal);
        }
    }
}
