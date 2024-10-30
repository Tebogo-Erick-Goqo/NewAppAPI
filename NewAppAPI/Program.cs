using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Rewrite;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ITaskService>(new InMemoryTaskService());

var app = builder.Build();

//Middleware
app.UseRewriter(new RewriteOptions().AddRedirect("tasks/(.*)", "todos/$1"));
app.Use(async (context, next) => {
    Console.WriteLine($"[{context.Request.Method} {context.Request.Path} {DateTime.UtcNow}] Started.");
    await next(context);
    Console.WriteLine($"[{context.Request.Method} {context.Request.Path} {DateTime.UtcNow}] Finished.");
});

var todos = new List<Todo>();

//Get
app.MapGet("/todos", (ITaskService service) => service.GetTodos());
app.MapGet("/todos/{id}", Results<Ok<Todo>, NotFound> (int id, ITaskService service) =>
{
    var targetTodo = service.GetTodoById(id);
    return targetTodo is null 
    ? TypedResults.NotFound() 
    : TypedResults.Ok(targetTodo);
});

//Post
app.MapPost("/todos", (Todo task, ITaskService service) =>
{
    service.AddTodo(task);
    return TypedResults.Created("/todos/{id}", task);
})
.AddEndpointFilter(async (context, next) => {
    var taskArgument = context.GetArgument<Todo>(0);
    var errors = new Dictionary<string, string[]>();
    if(taskArgument.DueDate < DateTime.UtcNow)
    {
        errors.Add(nameof(Todo.DueDate), ["Cannot have duedate in the past"]);
    }
    if (taskArgument.isCompleted)
    {
        errors.Add(nameof(Todo.isCompleted), ["Cannot Add completed todo"]);
    }

    if(errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    return await next(context);
});

//Delete
app.MapDelete("/todos/{id}", (int id, ITaskService service) => 
{
    service.DeleteTodoById(id);
    return TypedResults.NoContent();
});


app.Run();

public record Todo(int Id, string Name, DateTime DueDate, bool isCompleted)
{
    
}

interface ITaskService
{
    Todo? GetTodoById(int Id);
    List<Todo> GetTodos();
    void DeleteTodoById (int Id);
    Todo AddTodo (Todo task);
}


class InMemoryTaskService : ITaskService
{
    private readonly List<Todo> _todos = [];

    public Todo AddTodo(Todo task)
    {
        _todos.Add(task);
        return task;
    }

    public void DeleteTodoById(int Id)
    {
        _todos.RemoveAll(task => Id == task.Id);
    }

    public Todo? GetTodoById(int Id)
    {
        return _todos.FirstOrDefault(t => Id == t.Id);
    }

    public List<Todo> GetTodos()
    {
        return _todos;
    }
}