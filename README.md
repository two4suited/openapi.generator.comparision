# OpenAPI generation: TypeSpec vs ASP.NET Core

The same "Task API" (list / get / create / delete tasks) built two ways.

| | TypeSpec (`typespec/`) | ASP.NET Core (`aspnet/TaskApi/`) |
|---|---|---|
| Source of truth | `main.tsp` (the contract) | C# code (the implementation) |
| Output | `typespec/openapi.yaml` (OpenAPI 3.0) | `aspnet/TaskApi/TaskApi.json` (OpenAPI 3.2) |
| Generate | `cd typespec && npm i && npm run build` | `cd aspnet/TaskApi && dotnet build` |
| Runtime doc | n/a | `dotnet run` then `/openapi/v1.json` |

## Observed differences
- **Direction**: TypeSpec is design-first (spec -> emit OpenAPI -> implement server separately). ASP.NET is code-first (the spec is derived from endpoints/types).
- **Nullability / required**: TypeSpec `dueDate?` is simply optional. In ASP.NET, `DateOnly?` on a record still lands in `required` (the property is always serialized), so optionality needs extra care.
- **Enums**: ASP.NET emits `type: integer` unless you add `JsonStringEnumConverter`; TypeSpec emits string enums by default.
- **Responses**: TypeSpec unions (`TaskItem | NotFoundResponse`) vs. ASP.NET `Results<Ok<T>, NotFound>` typed results; plain return types only document `200`.
- **Versions/ops**: ASP.NET uses `WithName` for operationId; TypeSpec derives `Tasks_getTask` from interface + op.

## Web app with Kiota clients (`webapp/WebApp`)
Two [Kiota](https://learn.microsoft.com/openapi/kiota/) C# clients, one generated from each spec, used by a small web app
(`WebApp.Clients.TypeSpec` and `WebApp.Clients.AspNet`). Both talk to the same running Task API.

```bash
dotnet tool restore
# regenerate specs first (see above), then the clients:
dotnet kiota generate -l CSharp -d typespec/openapi.yaml -c TypeSpecClient -n WebApp.Clients.TypeSpec -o webapp/WebApp/Clients/TypeSpec --clean-output
dotnet kiota generate -l CSharp -d aspnet/TaskApi/TaskApi.json -c AspNetClient -n WebApp.Clients.AspNet -o webapp/WebApp/Clients/AspNet --clean-output

# run
(cd aspnet/TaskApi && dotnet run --urls http://localhost:5000) &
(cd webapp/WebApp && dotnet run)   # http://localhost:5100
```
The page shows two columns; adding a task in either column goes through that column's client.
The generated clients are near-identical here because the specs describe the same API.

## Aspire AppHost (`apphost/`)
Runs the Task API and the web app together (the web app gets the API URL via `TaskApiUrl`):

```bash
cd apphost && dotnet run --launch-profile https   # dashboard at https://localhost:17040
```
The `taskapi` resource has two dashboard commands:
- **Generate OpenAPI (TypeSpec)**: `npm install && npm run build` in `typespec/` -> `typespec/openapi.yaml`
- **Generate OpenAPI (from API)**: fetches `/openapi/v1.json` from the running API -> `aspnet/TaskApi/TaskApi.json`

The API also registers an OpenAPI operation transformer that fills in a missing `operationId`
(`Controller_Action`, or `METHOD_path` for minimal APIs). `ListTasks` deliberately has no `WithName`, so it comes out as `GET_tasks`.
Check operationIds with `node scripts/check-operation-ids.mjs` (fails on changed, missing or duplicate IDs; `--update` accepts intentional changes into `operation-ids.lock.json`).

`servicedefaults/` holds the shared Aspire ServiceDefaults project (OpenTelemetry logs/traces/metrics, health checks,
service discovery). Both `TaskApi` and `WebApp` call `AddServiceDefaults()` / `MapDefaultEndpoints()`. The web app is a
Blazor Server app (`webapp/WebApp/Components/Pages/Home.razor`) that injects both Kiota clients.
The AppHost sets `ASPNETCORE_ENVIRONMENT=Development` on both services because they have no `launchSettings.json`.
