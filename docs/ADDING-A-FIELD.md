# Adding a field — the ripple checklist

Adding one user-visible field to an entity touches up to nine artifacts across four projects
(the deliberate cost of the loopback self-API architecture). Every step is individually
trivial — which is exactly why steps get skipped. **The failure mode is silent:** a missed DTO
field or consumer update usually renders as a *blank value* in the UI, not an error, because
`ApiClientBase` degrades failures to the empty state. Walk the list top to bottom and check
each box.

## The checklist

1. **Entity** — `src/ResetYourFuture.Domain/Domain/Entities/` (or `Identity/ApplicationUser.cs`).
2. **EF configuration** — `src/ResetYourFuture.Infrastructure/Data/Configurations/` if the field
   needs a max length, required-ness, index, or conversion. Skippable for plain optional columns.
3. **Migration** — `dotnet ef migrations add <Name> --project src/ResetYourFuture.Infrastructure --startup-project src/ResetYourFuture.Web`.
   The always-on `MigrationChainTests.Model_HasNoPendingChanges_AgainstSqlServer` test fails the
   suite if you forget this step. Never let a restore rewrite csproj/`Directory.Packages.props`
   (the `Microsoft.OpenApi` auto-pin trap — `git checkout` them if it happens).
4. **DTO record(s)** — `src/ResetYourFuture.Application/DTOs/`. Positional records: adding a
   field mid-record breaks every construction site by position — prefer appending with a default
   value, or lean on step 5 so there is only *one* construction site to fix.
5. **Mapping helper** — `src/ResetYourFuture.Application/Mappings/`. One class per entity family;
   the in-query `Expression` member and its materialized twin sit side by side — **update both**
   (they share field order on purpose). If the DTO has no helper yet (single-construction-site
   DTOs deliberately don't), update its one projection in the service/controller.
6. **Controller** — usually free if the service projects via the mapping helper; check any
   endpoint that builds the DTO by hand or accepts the field on a `Save*Request`.
7. **Consumer** — `src/ResetYourFuture.Web/Consumers/`: the interface *and* the implementation
   if the method signature or query string changes. (For pure DTO-shape changes, nothing to do —
   consumers deserialize whatever the record declares.)
8. **Page / component** — markup in `src/ResetYourFuture.Web/Pages/` + code-behind. New admin
   table columns also need a sort key in the matching `Domain/Extensions/*SearchExtensions.cs`
   whitelist.
9. **Localization** — any new label needs **three** edits per resource family:
   `Shared/Resources/X.resx` (EN), `X.el.resx` (EL), and the hand-edited `X.Designer.cs`
   (the VS resx generator does not run under `dotnet build`).
10. **Tests** — extend the touched service/controller tests; a new sortable column gets a case in
    its `*SearchExtensionsTests`.

## Verify before calling it done

- `dotnet build ResetYourFuture.sln` and `dotnet test ResetYourFuture.sln` — green.
- Open the page in the browser (EN **and** EL if you added labels) and confirm the value actually
  renders — a blank cell where data should be means a missed step 4–7, not "no data".
- The e2e smoke suite (`tests/e2e/`, see its README) catches whole-page blank renders if the
  field broke a projection.
