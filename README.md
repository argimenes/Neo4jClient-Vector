# Neo4jClient-Vector
Neo4Client-Vector builds on the Neo4jClient API with a set of extension methods for generating Cypher patterns based on reflection, rather than hard-coded text. It also includes some helper methods for more concise queries, such as `FirstOrDefault()`, `ToList()`, `Count()`, and a generic pagination method.

## How it works
At the core of the library is the generic class `Vector<TRelation, TSource, TTarget>` which is a type-safe equivalent of a Cypher pattern. A Cypher pattern is a fragment of Cypher code that represents the relationship between two nodes, which is also called a path. In Cypher a path can encompass the traversal of a number of nodes, so in this project I have used the term 'vector' to mean specifically the path between just two nodes, that includes the source node, the relationship, and the target node. `Vector<TRelation, TSource, TTarget>` is essentially an abstraction representing `(a)-[r]->(b)` or `(a)<-[r]-(b)`.

## Getting started
The first step is to clone the repository, build it locally, and add a project reference to the Neo4jClient-Vector solution.

### Dependencies
- Install Neo4jClient Nuget package v.2.0.0.8

## An example
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

Now that we have a our own Vector we can write code like the following, utilising the library's extension methods:

```c#
public async Task<IEnumerable<Category>> ChildrenOfAsync(string code)
{
    var category = await FindByCodeAsync<Category>(code);
    var query = graph.From<Category>(category.Guid)
                     .OptMatch<KindOfCategory>(to: "child");
    return await query.ToListAsync(child => child.As<Category>(), orderBy: OrderBy.From(query).When("ByName", "child.Name"));
}

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

public async Task<SearchCategoryGraph> SearchAsync(SearchCategoryGraph query)
{
    var records = graph.From<Category>()
                       .OptMatch<KindOfCategory>(to: "parents")
                       .OptMatch<KindOfCategory>(rel: "", relPath: "*", to: "ancestors");
                       
    records = Filter(query, records);

    return await PageAsync<CategoryGraph, SearchCategoryGraph>(query, records,
        selector: c => new CategoryGraph
        {
            Entity = c.As<Category>(),
            Ancestors = Return.As<IEnumerable<string>>("collect(ancestors.Name)"),
            Parents = Return.As<IEnumerable<KindOfCategory>>(Collect<KindOfCategory>("parents"))
        },
        orderBy: OrderBy(query).When("ByName", "c.Name"));
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
