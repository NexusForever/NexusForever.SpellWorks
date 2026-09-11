using NexusForever.SpellWorks.Services;
using NexusForever.SpellWorks.Services.Filtering;
using Xunit;

namespace NexusForever.SpellWorks.Test.Services
{
    /// <summary>
    /// The query tree itself: cloning, in-place rehydration and the change signature. Everything here is
    /// about the object graph, not about what it filters.
    /// </summary>
    public class FilterQueryTests
    {
        [Fact]
        public void A_fresh_query_is_empty_and_counts_nothing()
        {
            var query = new FilterQuery();

            Assert.True(query.IsEmpty);
            Assert.Equal(0, query.ConditionCount);
            Assert.Empty(query.Groups);
        }

        [Fact]
        public void The_condition_count_spans_the_common_band_and_every_block()
        {
            FilterQuery query = Query();
            query.Common.Conditions.Add(Condition(FilterFields.Deprecated));

            Assert.Equal(3, query.ConditionCount);
        }

        [Fact]
        public void Cloning_is_deep_so_the_background_projection_cannot_see_later_edits()
        {
            // The clone is handed to a worker thread while the form keeps editing. A MemberwiseClone would
            // share the very lists being edited - this is the test that would have caught that.
            FilterQuery original = Query();
            FilterQuery clone = original.Clone();

            clone.Groups[0].Conditions[0].Value = "changed";
            clone.Groups[0].Conditions.Add(Condition(FilterFields.HasProcs));
            clone.Groups.Add(new FilterGroup());
            clone.Search = "changed";

            Assert.Equal("Fire", original.Groups[0].Conditions[0].Value);
            Assert.Single(original.Groups[0].Conditions);
            Assert.Equal(2, original.Groups.Count);
            Assert.Equal("arcane", original.Search);
        }

        [Fact]
        public void CopyFrom_refills_in_place_rather_than_replacing_the_lists()
        {
            // PaneState.Filters is get-only and the form holds references into it, so rehydration has to
            // land in the object the views already captured.
            var live = new FilterQuery();
            FilterGroup captured = live.Common;

            live.CopyFrom(Query());

            Assert.Same(captured, live.Common);
            Assert.Equal(2, live.Groups.Count);
            Assert.Equal("arcane", live.Search);
        }

        [Fact]
        public void CopyFrom_null_clears_rather_than_throwing()
        {
            FilterQuery live = Query();

            live.CopyFrom(null);

            Assert.True(live.IsEmpty);
        }

        [Fact]
        public void CopyFrom_caps_the_blocks_and_copies_every_condition_in_them()
        {
            // Blocks are capped; conditions within one are not. A copy that dropped conditions would make
            // the preview count and the applied filter answer different questions.
            var wide = new FilterQuery();
            for (int i = 0; i < FilterQuery.MaxGroups + 5; i++)
            {
                var group = new FilterGroup();
                for (int j = 0; j < 21; j++)
                    group.Conditions.Add(Condition(FilterFields.Id));

                wide.Groups.Add(group);
            }

            var live = new FilterQuery();
            live.CopyFrom(wide);

            Assert.Equal(FilterQuery.MaxGroups, live.Groups.Count);
            Assert.All(live.Groups, g => Assert.Equal(21, g.Conditions.Count));
        }

        [Fact]
        public void Reset_keeps_the_search_and_ResetAll_does_not()
        {
            FilterQuery query = Query();

            query.Reset();
            Assert.Equal("arcane", query.Search);
            Assert.Empty(query.Groups);

            query = Query();
            query.ResetAll();
            Assert.Equal("", query.Search);
        }

        // ------------------------------------------------------------------ signature

        [Fact]
        public void The_signature_changes_when_any_part_of_the_query_does()
        {
            FilterQuery query = Query();
            string before = query.Signature();

            query.Groups[0].Conditions[0].Value = "Ice";
            Assert.NotEqual(before, query.Signature());
        }

        [Fact]
        public void The_signature_changes_when_a_condition_is_negated()
        {
            FilterQuery query = Query();
            string before = query.Signature();

            query.Groups[0].Conditions[0].Negate = true;
            Assert.NotEqual(before, query.Signature());
        }

        [Fact]
        public void The_signature_changes_when_blocks_are_reordered()
        {
            // Block order is user-visible, so it has to invalidate. A spurious reload is far cheaper than
            // a missed one.
            FilterQuery query = Query();
            query.Groups[1].Conditions.Add(Condition(FilterFields.Id, "7157"));

            string before = query.Signature();
            (query.Groups[0], query.Groups[1]) = (query.Groups[1], query.Groups[0]);

            Assert.NotEqual(before, query.Signature());
        }

        [Fact]
        public void Values_cannot_forge_a_signature_boundary()
        {
            // Without a separator, Id="7" + Cast="157" and Id="71" + Cast="57" would produce the same string
            // and the grid would silently skip the reload.
            var left = new FilterQuery();
            left.Groups.Add(new FilterGroup());
            left.Groups[0].Conditions.Add(Condition(FilterFields.Id, "7"));
            left.Groups[0].Conditions.Add(Condition(FilterFields.CastMethod, "157"));

            var right = new FilterQuery();
            right.Groups.Add(new FilterGroup());
            right.Groups[0].Conditions.Add(Condition(FilterFields.Id, "71"));
            right.Groups[0].Conditions.Add(Condition(FilterFields.CastMethod, "57"));

            Assert.NotEqual(left.Signature(), right.Signature());
        }

        [Fact]
        public void A_group_split_changes_the_signature_even_with_the_same_conditions()
        {
            var together = new FilterQuery();
            together.Groups.Add(new FilterGroup());
            together.Groups[0].Conditions.Add(Condition(FilterFields.Id, "7"));
            together.Groups[0].Conditions.Add(Condition(FilterFields.Id, "8"));

            var apart = new FilterQuery();
            apart.Groups.Add(new FilterGroup());
            apart.Groups[0].Conditions.Add(Condition(FilterFields.Id, "7"));
            apart.Groups.Add(new FilterGroup());
            apart.Groups[1].Conditions.Add(Condition(FilterFields.Id, "8"));

            Assert.NotEqual(together.Signature(), apart.Signature());
        }

        [Fact]
        public void An_identical_query_signs_identically()
        {
            Assert.Equal(Query().Signature(), Query().Signature());
            Assert.Equal(Query().Signature(), Query().Clone().Signature());
        }

        [Fact]
        public void A_clone_carries_the_common_band_too()
        {
            // The common band narrows every block, so a clone that dropped it would hand the projection
            // thread a query that matches strictly more than the form is showing.
            FilterQuery original = Query();
            original.Common.Conditions.Add(Condition(FilterFields.Deprecated));

            FilterQuery clone = original.Clone();

            FilterCondition copied = Assert.Single(clone.Common.Conditions);
            Assert.Equal(FilterFields.Deprecated, copied.Field);
            Assert.NotSame(original.Common.Conditions[0], copied);
        }

        [Fact]
        public void Rehydrating_refills_the_common_band_too()
        {
            FilterQuery saved = Query();
            saved.Common.Conditions.Add(Condition(FilterFields.Deprecated));

            var live = new FilterQuery();
            live.CopyFrom(saved);

            FilterCondition loaded = Assert.Single(live.Common.Conditions);
            Assert.Equal(FilterFields.Deprecated, loaded.Field);
            Assert.NotSame(saved.Common.Conditions[0], loaded);
        }

        // ------------------------------------------------------------------ editing guard rails

        [Fact]
        public void A_query_that_is_already_full_gains_no_further_block()
        {
            var query = new FilterQuery();
            for (int i = 0; i < FilterQuery.MaxGroups; i++)
                query.AddGroup();

            FilterGroup last = query.Groups[^1];

            Assert.Same(last, query.AddGroup());
            Assert.Equal(FilterQuery.MaxGroups, query.Groups.Count);
        }

        [Fact]
        public void Making_a_condition_local_leaves_a_query_it_is_not_in_alone()
        {
            FilterQuery query = Query();
            string before = query.Signature();

            query.MakeLocal(Condition(FilterFields.Id, "7"));

            Assert.Equal(before, query.Signature());
        }

        [Fact]
        public void Pruning_against_no_schema_prunes_nothing()
        {
            // A pane with no form has no schema to judge a condition by, so the query is left as it is
            // rather than being emptied.
            FilterQuery query = Query();
            string before = query.Signature();

            query.Prune(null);

            Assert.Equal(before, query.Signature());
            Assert.Equal(2, query.Groups.Count);
        }

        // ------------------------------------------------------------------ helpers

        internal static FilterCondition Condition(string field, string value = "", bool negate = false) =>
            new() { Field = field, Value = value, Negate = negate };

        /// <summary>(school = Fire) OR (has procs), searching "arcane".</summary>
        private static FilterQuery Query()
        {
            var query = new FilterQuery { Search = "arcane" };

            var first = new FilterGroup();
            first.Conditions.Add(Condition(FilterFields.School, "Fire"));
            query.Groups.Add(first);

            var second = new FilterGroup();
            second.Conditions.Add(Condition(FilterFields.HasProcs));
            query.Groups.Add(second);

            return query;
        }
    }
}
