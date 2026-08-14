# NHibernate Issue Reproduction

Reproduces an issue discovered in NHibernate when LINQ queries are evaluated and `.Contains()` is called on a `string[]`.

Example of code that fails:

```csharp
var names = new string[] { "Widget", "Gadget" };

var results = session.Query<Product>()
  .Where(p => names.Contains(p.Name))
  .ToList();
```

This, however, works correctly:

```csharp
var names = new[] { "Widget", "Gadget" };

var results = session.Query<Product>()
  .Where(p => names.Contains(p.Name))
  .ToList();
```
