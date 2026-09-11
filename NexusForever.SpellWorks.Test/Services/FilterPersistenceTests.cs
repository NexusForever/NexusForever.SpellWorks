using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using NexusForever.SpellWorks.Services;
using NexusForever.SpellWorks.Services.Filtering;
using NexusForever.SpellWorks.Test.Testing;
using Xunit;

namespace NexusForever.SpellWorks.Test.Services
{
    /// <summary>
    /// Filters surviving a restart, and the three ways a saved condition can go stale.
    /// </summary>
    public class FilterPersistenceTests : IDisposable
    {
        private readonly FakeTableCatalog _catalog = new();
        private readonly FakeSpellModelService _models = new();
        private readonly string _directory;

        public FilterPersistenceTests()
        {
            _directory = Path.Combine(Path.GetTempPath(), "SpellWorks.Filters", Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(_directory);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
            }
        }

        private WorkspaceState NewState() => new(new WeakReferenceMessenger(), _models, _catalog);

        private FilterSchemaRegistry Schemas => new(_models, _catalog, new Preferences());

        private WorkspaceStore Store(WorkspaceState state) => new(state, Schemas, _directory);

        private string WorkspaceFile => Path.Combine(_directory, "Workspace.json");

        [Fact]
        public void A_query_survives_a_round_trip()
        {
            WorkspaceState saved = NewState();
            FilterQuery query = saved.PaneStateFor("spells").Filters;
            query.Search = "fire && !proxy";
            query.And(FilterFields.School, "Magic");
            query.Or(FilterFields.Class, "Esper");
            query.MakeCommon(new FilterCondition
            {
                Field = FilterFields.Deprecated, Operator = FilterOperator.IsSet, Negate = true
            });

            Store(saved).Save();

            WorkspaceState loaded = NewState();
            Store(loaded).Load();

            FilterQuery back = loaded.PaneStateFor("spells").Filters;
            Assert.Equal("fire && !proxy", back.Search);
            Assert.Equal(2, back.Groups.Count);
            Assert.Equal("Magic", back.Groups[0].Conditions.Single().Value);
            Assert.Equal("Esper", back.Groups[1].Conditions.Single().Value);
            Assert.True(back.Common.Conditions.Single().Negate);
        }

        [Fact]
        public void Both_search_boxes_and_the_exact_toggle_survive_a_round_trip()
        {
            WorkspaceState saved = NewState();
            FilterQuery query = saved.PaneStateFor("spells").Filters;
            query.Search      = "arcane";
            query.IdSearch    = "7157";
            query.ExactSearch = true;

            Store(saved).Save();

            WorkspaceState loaded = NewState();
            Store(loaded).Load();

            FilterQuery back = loaded.PaneStateFor("spells").Filters;
            Assert.Equal("arcane", back.Search);
            Assert.Equal("7157", back.IdSearch);
            Assert.True(back.ExactSearch);
        }

        [Fact]
        public void An_id_search_alone_is_enough_to_be_worth_saving()
        {
            WorkspaceState saved = NewState();
            saved.PaneStateFor("spells").Filters.IdSearch = "7157";

            Store(saved).Save();

            Assert.Contains("idSearch", File.ReadAllText(WorkspaceFile));
        }

        [Fact]
        public void A_workspace_written_before_the_split_still_loads()
        {
            // The text box kept its key, so an older file's search lands where it always did.
            Write("""
                {
                  "Open": ["spells"],
                  "Filters": { "spells": { "search": "arcane" } }
                }
                """);

            WorkspaceState loaded = NewState();
            Store(loaded).Load();

            FilterQuery back = loaded.PaneStateFor("spells").Filters;
            Assert.Equal("arcane", back.Search);
            Assert.Equal("", back.IdSearch);
            Assert.False(back.ExactSearch);
        }

        [Fact]
        public void The_operator_is_written_by_name_so_renumbering_cannot_corrupt_a_file()
        {
            WorkspaceState saved = NewState();
            saved.PaneStateFor("spells").Filters.Set(FilterFields.Id, "7157", FilterOperator.StartsWith);

            Store(saved).Save();

            Assert.Contains("\"StartsWith\"", File.ReadAllText(WorkspaceFile));
        }

        [Fact]
        public void An_empty_query_writes_nothing()
        {
            WorkspaceState saved = NewState();
            saved.PaneStateFor("spells");

            Store(saved).Save();

            Assert.DoesNotContain("\"Filters\"", File.ReadAllText(WorkspaceFile));
        }

        [Fact]
        public void A_dead_scope_is_pruned_rather_than_accumulating_forever()
        {
            // detail:2 and its kin are spawned liberally as the user follows cross-references.
            WorkspaceState saved = NewState();
            saved.PaneStateFor("detail:2").Filters.Set(FilterFields.Id, "9", FilterOperator.StartsWith);
            saved.PaneStateFor("spells").Filters.Set(FilterFields.Id, "7", FilterOperator.StartsWith);

            Store(saved).Save();
            string json = File.ReadAllText(WorkspaceFile);

            Assert.Contains("spells", json);
            Assert.DoesNotContain("detail:2", json);
        }

        [Fact]
        public void An_open_spawned_scope_is_kept()
        {
            WorkspaceState saved = NewState();
            saved.SelectView("detail:2");
            saved.PaneStateFor("detail:2").Filters.Set(FilterFields.Id, "9", FilterOperator.StartsWith);

            Store(saved).Save();

            Assert.Contains("detail:2", File.ReadAllText(WorkspaceFile));
        }

        [Fact]
        public void An_unknown_field_key_is_dropped_without_losing_the_rest()
        {
            // Structurally meaningless: it can be neither rendered nor repaired.
            Write("""
                {
                  "Open": ["spells"],
                  "Filters": {
                    "spells": {
                      "groups": [[
                        { "field": "school", "op": "Equals", "value": "Magic" },
                        { "field": "field.that.went.away", "op": "Equals", "value": "x" }
                      ]]
                    }
                  }
                }
                """);

            WorkspaceState loaded = NewState();
            Store(loaded).Load();

            FilterCondition condition = Assert.Single(loaded.PaneStateFor("spells").Filters.Groups[0].Conditions);
            Assert.Equal(FilterFields.School, condition.Field);
        }

        [Fact]
        public void An_operator_the_field_no_longer_offers_is_coerced_to_its_default()
        {
            // Coercing preserves the intent as closely as anything can; dropping would lose it entirely.
            Write("""
                {
                  "Open": ["spells"],
                  "Filters": {
                    "spells": {
                      "groups": [[ { "field": "id", "op": "MaskAny", "value": "7157" } ]]
                    }
                  }
                }
                """);

            WorkspaceState loaded = NewState();
            Store(loaded).Load();

            FilterCondition condition = loaded.PaneStateFor("spells").Filters.Groups[0].Conditions.Single();
            Assert.Equal(FilterOperator.StartsWith, condition.Operator);
            Assert.Equal("7157", condition.Value);
        }

        [Fact]
        public void A_value_that_no_longer_parses_is_kept_so_it_can_be_seen_and_fixed()
        {
            // A constraint that silently vanished is worse than one that is visibly wrong.
            Write("""
                {
                  "Open": ["spells"],
                  "Filters": {
                    "spells": {
                      "groups": [[ { "field": "school", "op": "Equals", "value": "NoSuchSchool" } ]]
                    }
                  }
                }
                """);

            WorkspaceState loaded = NewState();
            Store(loaded).Load();

            FilterCondition condition = loaded.PaneStateFor("spells").Filters.Groups[0].Conditions.Single();
            Assert.Equal("NoSuchSchool", condition.Value);
            Assert.False(Schemas.For(PaneDescriptor.Spell4).Field(FilterFields.School).IsValid(condition));
        }

        [Fact]
        public void A_block_that_loses_every_condition_is_not_kept_as_an_empty_block()
        {
            Write("""
                {
                  "Open": ["spells"],
                  "Filters": {
                    "spells": {
                      "groups": [
                        [ { "field": "school", "op": "Equals", "value": "Magic" } ],
                        [ { "field": "gone", "op": "Equals", "value": "x" } ]
                      ]
                    }
                  }
                }
                """);

            WorkspaceState loaded = NewState();
            Store(loaded).Load();

            Assert.Single(loaded.PaneStateFor("spells").Filters.Groups);
        }

        [Fact]
        public void A_corrupt_entry_costs_its_own_pane_and_not_the_layout()
        {
            Write("""
                {
                  "Open": ["spells", "tables"],
                  "Active": "tables",
                  "Filters": {
                    "spells": { "groups": [[ { "field": "school", "op": "Equals", "value": "Magic" } ]] }
                  }
                }
                """);

            WorkspaceState loaded = NewState();
            Store(loaded).Load();

            Assert.Equal(["spells", "tables"], loaded.Open);
            Assert.Equal("tables", loaded.Active);
        }

        [Fact]
        public void A_workspace_with_no_filters_section_loads_as_before()
        {
            Write("""{ "Open": ["spells"], "Active": "spells" }""");

            WorkspaceState loaded = NewState();
            Store(loaded).Load();

            Assert.True(loaded.PaneStateFor("spells").Filters.IsEmpty);
        }

        [Fact]
        public void A_hand_edited_file_cannot_exceed_the_block_guard_rail()
        {
            // Blocks are still capped - the compiler folds them into a disjunction and the form draws every
            // card in each. Conditions within a block are not.
            var groups = new List<List<FilterConditionDto>>();
            for (int i = 0; i < FilterQuery.MaxGroups + 4; i++)
            {
                var conditions = new List<FilterConditionDto>();
                for (int j = 0; j < 20; j++)
                    conditions.Add(new FilterConditionDto
                    {
                        Field = FilterFields.Id, Operator = FilterOperator.StartsWith, Value = "7"
                    });

                groups.Add(conditions);
            }

            var query = new FilterQuery();
            FilterQueryDtoMapper.Load(query, new FilterQueryDto { Groups = groups },
                Schemas.For(PaneDescriptor.Spell4));

            Assert.Equal(FilterQuery.MaxGroups, query.Groups.Count);
            Assert.All(query.Groups, g => Assert.Equal(20, g.Conditions.Count));
        }

        [Fact]
        public void A_block_of_many_conditions_survives_a_restart()
        {
            // The form draws more than sixteen fields and typing into one creates its condition, so a
            // block this size is ordinary and nothing may be truncated on the way back in.
            WorkspaceState saved = NewState();
            FilterQuery query = saved.PaneStateFor("spells").Filters;

            for (int i = 0; i < 20; i++)
                query.And(FilterFields.Id, i.ToString(), FilterOperator.StartsWith);

            Store(saved).Save();

            WorkspaceState loaded = NewState();
            Store(loaded).Load();

            FilterGroup block = Assert.Single(loaded.PaneStateFor("spells").Filters.Groups);
            Assert.Equal(20, block.Conditions.Count);
        }

        [Fact]
        public void A_large_common_band_survives_a_restart()
        {
            // Pinning is unbounded, so the band can hold any number of conditions.
            WorkspaceState saved = NewState();
            FilterQuery query = saved.PaneStateFor("spells").Filters;

            for (int i = 0; i < 20; i++)
                query.MakeCommon(new FilterCondition
                {
                    Field = FilterFields.Id, Operator = FilterOperator.StartsWith, Value = i.ToString()
                });

            Store(saved).Save();

            WorkspaceState loaded = NewState();
            Store(loaded).Load();

            Assert.Equal(20, loaded.PaneStateFor("spells").Filters.Common.Conditions.Count);
        }

        [Fact]
        public void A_pane_with_no_form_persists_nothing()
        {
            var query = new FilterQuery();
            query.Set(FilterFields.Id, "7157");

            FilterQueryDtoMapper.Load(query, FilterQueryDtoMapper.ToDto(query), Schemas.For(PaneDescriptor.Setup));

            Assert.True(query.IsEmpty);
        }

        private void Write(string json) => File.WriteAllText(WorkspaceFile, json);
    }
}
