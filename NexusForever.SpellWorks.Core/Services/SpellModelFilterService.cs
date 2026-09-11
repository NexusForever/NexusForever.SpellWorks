using NexusForever.SpellWorks.Core.Models;
using NexusForever.SpellWorks.Core.Models.Filter;

namespace NexusForever.SpellWorks.Core.Services
{
    public class SpellModelFilterService : ISpellModelFilterService
    {
        public IEnumerable<ISpellModel> Filter(IEnumerable<IModelFilter<ISpellModel>> filters, IEnumerable<ISpellModel> models)
        {
            // A list of filters is a conjunction, which is exactly what AllOf is
            return Filter(new AllOfFilter<ISpellModel>(filters), models);
        }

        public IEnumerable<T> Filter<T>(IModelFilter<T> filter, IEnumerable<T> models)
        {
            foreach (T model in models)
                if (filter.Filter(model))
                    yield return model;
        }
    }
}
