# Neo4jClient-Vector
Neo4Client-Vector builds on the Neo4jClient API with a set of extension methods for generating Cypher patterns based on reflection, rather than hard-coded text. It also includes some helper methods for more concise queries, such as `FirstOrDefault()`, `ToList()`, `Count()`, and a generic pagination method.

## How it works
At the core of the library is the generic class `Vector<TRelation, TSource, TTarget>` which is a type-safe equivalent of a Cypher pattern. A Cypher pattern is a fragment of Cypher code that represents the relationship between two nodes, which is also called a path. In Cypher a path can encompass the traversal of a number of nodes, so in this project I have used the term 'vector' to mean specifically the path between just two nodes, that includes the source node, the relationship, and the target node. `Vector<TRelation, TSource, TTarget>` is essentially an abstraction representing `(a)-[r]->(b)` or `(a)<-[r]-(b)`.

## Getting started
The first step is to clone the repository, build it locally, and add a project reference to the Neo4jClient-Vector solution.

### Dependencies
- Install Neo4jClient Nuget package v.2.0.0.8

## Some examples
There are a number of core classes in Neo4jClient-Vector that you will need to inherit from to use the project. The `Entity` close represents a Cypher node and the `Relation` class represents a Cypher relationship. These classes can be decorated with the attributes `Relationship` and `Node`. `Relationship` is the most important one is it is required for defining the properties of the Cypher vector pattern.

Here is a typical example of how you could use it:

```c#
[Relationship(Type = "kind_of_category", Direction = RelationshipDirection.Outgoing)]
public class KindOfCategoryRelation : Relation { }
```

This example is equivalent to writing the following Cypher pattern:

```cypher
()-[:kind_of_category]->()
```

To make a proper Vector, though, we will also need to define any node classes.

```c#
[Node(Name = "cat")]
public class Category : Entity
{
    public string Description { get; set; }
}
```

Now that we have a `Relation` and `Entity` defined we can create a Vector:

```c#
public class KindOfCategory : Vector<KindOfCategoryRelation, Category, Category> { }
```

This represents the following Cypher pattern:

```cypher
(:Category)-[:kind_of_category]->(:Category)
```

Now that we have a our own Vector we can write code like the following, utilising the library's extension methods. This example demonstrates Cypher path generation with `OptMatch<T>` and a shortcut `ToListAsync` method:

```c#
public async Task<IEnumerable<Category>> ChildrenOfAsync(string code)
{
    var query = graph.From<Concept>(concept.Guid)
                     .OptMatch<ChildrenOfConcept>(to: "children");
    var list = await query.ToListAsync(children => children.As<Concept>(), orderBy: "children.Name");
    return list ?? new List<Concept>();
}
```

The following example takes a node identifier (a readable string code name in this case) and returns all of the children of that node, and includes the ancestors of each child for use in a drop-down list using the `Path` method.

```c#
public async Task<IEnumerable<AncestorsOfConcept>> DescendantsOfAsync(string ancestorCode = null)
{
    var root = ancestorCode.HasValue() ? FromCode<Concept>(ancestorCode) : From<Concept>();
    var query = root.Match<ChildrenOfConcept>(relPath: "*0..", to: "d").With("d")
                    .Path<SubsetOfConcept>(from: "d", relPath: "*0..", to: "a").Where("a.Code", ancestorCode)
                    .With("d, nodes(p) as n").Unwind("n", "ancestors");
    return await query
                .ToListAsync(d => new AncestorsOfConcept
                {
                    Entity = d.As<Concept>(),
                    Ancestors = Return.As<IEnumerable<string>>("tail(collect(distinct(ancestors.Name)))")
                },
                orderBy: "d.Name ASC");
}
```

The example below returns a `Graph<T>` (i.e., a sub-graph centred on a given `Category` node):

```c#
public async Task<CategoryGraph> FindGraphAsync(Guid guid)
{
    return await graph.From<Category>(guid)
                      .OptMatch<KindOfCategory>(to: "parents")
                      .FirstOrDefaultAsync(c => new CategoryGraph
                      {
                          Entity = c.As<Category>(),
                          Parents = Return.As<IEnumerable<KindOfCategory>>(Collect<KindOfCategory>("parents"))
                      });
}
```

This example demonstrates the `PageAsync` function, along with functions that take the search query object and build a Cypher query dynamically. `If` allows you to add to the `ICypherFluentQuery` object based on filter conditions, whereas `PageAsync` takes the query object and handles pagination, setting row counts, the maximum page value, execution time, etc. Note also how the use of `Match<TypeOfAgent, SubsetOfConcept>` to easily build a hyper-node expression (i.e., a Cypher pattern that transitions across two edges). The functions `WhereLike` and `AndWhereLike` allow you to execute case-insensitive partial matches, and the `OrderBy` object shown in the `PageAsync` call allows you to dynamically construct an `ORDER BY` Cypher statement. 

```c#
public async Task<SearchAgentCluster> SearchAsync(SearchAgentCluster query)
{
    var records = From<Agent>().Where()
                    .If(query.Name.HasValue(), x => x.AndWhereLike("a.Name", query.Name))
                    .If(query.FirstName.HasValue(), x => x.AndWhereLike("a.FirstName", query.FirstName))
                    .If(query.LastName.HasValue(), x => x.AndWhereLike("a.LastName", query.LastName))
                    .If(query.IsDeleted.HasValue, x => x.AndWhere("a.IsDeleted", query.IsDeleted.ToString()))
                    .If(query.Type.HasValue, x => x.With("a")
                        .Match<TypeOfAgent, SubsetOfConcept>(to: "type", relPath2: "*0..", to2: "ancestor").Where("ancestor.Guid", query.Type)
                        .With("a, type"))
                    .OptMatch<TypeOfAgent>(to: "type");

    return await PageAsync<AgentCluster, SearchAgentCluster>(query, records,
        a => new AgentCluster
        {
            Entity = a.As<Agent>(),
            Types = Return.As<IEnumerable<Concept>>("collect(distinct(type))")
        },
        orderBy: OrderBy.From(query)
                        .When(SearchOrder.ByName.ToString(), "a.Name, a.LastName, a.FirstName ASC", "a.Name, a.LastName DESC, a.FirstName ASC")
                        .When(SearchOrder.ByDateAdded.ToString(), "a.DateAddedUTC")
        );
}
```

Saving and updating entities has also been abstracted away, with optional Actions for inserting and updating the entity prior to committing:

```c#
public async Task<Result> SaveOrUpdateAsync(Category data)
{
    return await SaveOrUpdateAsync(data, update: x =>
    {
        x.Name = data.Name;
        x.Code = data.Code;
    });
}
```

There are also a couple of ways of updating relationships. If you just want to create a relationship between existing nodes you can do so as follows:

```c#
public async Task<Result> BelongsToCalendarAsync(Guid calendarEventId, Guid calendarId)
{
    return await RelateAsync(new BelongsToCalendar
    {
        Source = new CalendarEvent { Guid = calendarEventId },
        Target = new Calendar { Guid = calendarId }
    });
}
```

Or if you need to change a relationship from one node to a different node, you can do so using the `VectorIdent` class:

```c#
public async Task<Result> AtPlaceAsync(VectorIdent ident)
{
    if (ident.RelationId.HasValue)
    {
        await DeleteAsync<AtPlace>(ident);
    }
    return await RelateAsync<AtPlace>(ident);
}
```

## Coming soon

- A breakdown of the core classes, and their built-in properties

- Some more examples of how to access the helper functions
