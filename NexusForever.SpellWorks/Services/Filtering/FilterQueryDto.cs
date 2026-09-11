using System.Text.Json.Serialization;

namespace NexusForever.SpellWorks.Services.Filtering
{
    /// <summary>One persisted condition. A group is a bare array of these.</summary>
    /// <remarks>
    /// <c>op</c> serialises by name, never by number, so renumbering <see cref="FilterOperator"/> can never
    /// silently reinterpret a saved file. Defaults are omitted, so the common case writes almost nothing.
    /// </remarks>
    public sealed class FilterConditionDto
    {
        [JsonPropertyName("field")]
        public string Field { get; set; }

        [JsonPropertyName("op")]
        [JsonConverter(typeof(JsonStringEnumConverter<FilterOperator>))]
        public FilterOperator Operator { get; set; }

        [JsonPropertyName("value")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Value { get; set; }

        [JsonPropertyName("not")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Negate { get; set; }
    }

    /// <summary>One pane's persisted filter, keyed in the workspace file by pane scope.</summary>
    public sealed class FilterQueryDto
    {
        [JsonPropertyName("search")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Search { get; set; }

        [JsonPropertyName("idSearch")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string IdSearch { get; set; }

        [JsonPropertyName("exact")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool ExactSearch { get; set; }

        [JsonPropertyName("common")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<FilterConditionDto> Common { get; set; }

        [JsonPropertyName("groups")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<List<FilterConditionDto>> Groups { get; set; }
    }

    /// <summary>
    /// Between the live query and its persisted shape.
    /// </summary>
    /// <remarks>
    /// The three ways a saved condition can be stale are handled differently on purpose:
    /// an <em>unknown field key</em> is dropped, because it is structurally meaningless and can be neither
    /// rendered nor repaired; an <em>unknown operator</em> is coerced to the field's default, which preserves
    /// the intent as closely as anything can; and a <em>value that no longer parses</em> is kept and marked,
    /// because a constraint that silently vanished is worse than one the user can see and fix.
    ///
    /// Only <see cref="FilterQuery.MaxGroups"/> clips on the way in. Conditions within a block do not:
    /// the form creates a condition from typing into any of its fields, so clipping here would lose work
    /// the user could see themselves do.
    /// </remarks>
    public static class FilterQueryDtoMapper
    {
        public static FilterQueryDto ToDto(FilterQuery query)
        {
            if (query == null || query.IsEmpty)
                return null;

            return new FilterQueryDto
            {
                Search      = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search,
                IdSearch    = string.IsNullOrWhiteSpace(query.IdSearch) ? null : query.IdSearch,
                ExactSearch = query.ExactSearch,
                Common = query.Common.Conditions.Count > 0 ? [.. query.Common.Conditions.Select(ToDto)] : null,
                Groups = query.Groups.Count > 0
                    ? [.. query.Groups.Select(g => g.Conditions.Select(ToDto).ToList())]
                    : null
            };
        }

        /// <summary>
        /// Rehydrate <paramref name="query"/> in place from <paramref name="dto"/>, validated against
        /// <paramref name="schema"/>. A null schema means the pane kind offers no form, so nothing is loaded.
        /// </summary>
        public static void Load(FilterQuery query, FilterQueryDto dto, FilterSchema schema)
        {
            query.ResetAll();

            if (dto == null || schema == null)
                return;

            query.Search      = dto.Search ?? "";
            query.IdSearch    = dto.IdSearch ?? "";
            query.ExactSearch = dto.ExactSearch;

            foreach (FilterCondition condition in Read(dto.Common, schema))
                query.Common.Conditions.Add(condition);

            foreach (List<FilterConditionDto> group in (dto.Groups ?? []).Take(FilterQuery.MaxGroups))
            {
                List<FilterCondition> conditions = Read(group, schema).ToList();

                // A block that lost every condition to a schema change is not a block any more.
                if (conditions.Count == 0)
                    continue;

                var live = new FilterGroup();
                live.Conditions.AddRange(conditions);
                query.Groups.Add(live);
            }
        }

        private static FilterConditionDto ToDto(FilterCondition condition) => new()
        {
            Field    = condition.Field,
            Operator = condition.Operator,
            Value    = string.IsNullOrEmpty(condition.Value) ? null : condition.Value,
            Negate   = condition.Negate
        };

        private static IEnumerable<FilterCondition> Read(List<FilterConditionDto> dtos, FilterSchema schema)
        {
            foreach (FilterConditionDto dto in dtos ?? [])
            {
                FilterFieldSchema field = schema.Field(dto?.Field);
                if (field == null)
                    continue;

                yield return new FilterCondition
                {
                    Field    = field.Key,
                    Operator = field.AllowedOperators.Contains(dto.Operator) ? dto.Operator : field.DefaultOperator,
                    Value    = dto.Value ?? "",
                    Negate   = dto.Negate
                };
            }
        }
    }
}
