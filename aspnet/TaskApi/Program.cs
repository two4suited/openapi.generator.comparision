using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddOpenApi(options =>
{
    // Fill in operationId for any endpoint that doesn't set one via WithName().
    options.AddOperationTransformer((operation, context, ct) =>
    {
        if (!string.IsNullOrEmpty(operation.OperationId))
            return Task.CompletedTask; // explicit WithName wins

        var desc = context.Description;
        operation.OperationId = desc.ActionDescriptor switch
        {
            ControllerActionDescriptor c => $"{c.ControllerName}_{c.ActionName}",
            _ => Slug($"{desc.HttpMethod}_{desc.RelativePath}"),
        };
        return Task.CompletedTask;
    });
});

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapOpenApi(); // served at /openapi/v1.json

var tasks = new Dictionary<int, TaskItem>();
var nextId = 1;

var group = app.MapGroup("/tasks").WithTags("Tasks");

group.MapGet("/", (TaskStatus? status) =>
        tasks.Values.Where(t => status is null || t.Status == status))
    // no WithName: the transformer derives the operationId (GET_tasks)
    .WithSummary("List tasks, optionally filtered by status");

group.MapGet("/{id:int}", Results<Ok<TaskItem>, NotFound> (int id) =>
        tasks.TryGetValue(id, out var t) ? TypedResults.Ok(t) : TypedResults.NotFound())
    .WithName("GetTask");

group.MapPost("/", (CreateTask body, [FromHeader(Name = "X-Client")] string? client) =>
    {
        var task = new TaskItem(nextId++, body.Title, TaskStatus.Todo, body.DueDate, client);
        tasks[task.Id] = task;
        return TypedResults.Created($"/tasks/{task.Id}", task);
    })
    .WithName("CreateTask");

group.MapDelete("/{id:int}", Results<NoContent, NotFound> (int id) =>
        tasks.Remove(id) ? TypedResults.NoContent() : TypedResults.NotFound())
    .WithName("DeleteTask");

app.Run();

static string Slug(string s) => Regex.Replace(s, "[^A-Za-z0-9]+", "_").Trim('_');

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum TaskStatus { Todo, InProgress, Done }

/// <summary>A task.</summary>
public record TaskItem(int Id, string Title, TaskStatus Status, DateOnly? DueDate, string? CreatedBy);

/// <summary>Payload for creating a task.</summary>
public record CreateTask([property: Required, MinLength(1), MaxLength(100)] string Title, DateOnly? DueDate);
