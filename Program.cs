using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Rewrite;
using Dapper;
using Npgsql;
using System.Threading.Tasks;
using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);

// Add the Dapper Task Service with PostgreSQL connection
string connectionString = "Host=summme-prod.c4ot1f1mj7dd.us-east-2.rds.amazonaws.com;Port=5432;Database=summme_prod;Username=Hotkey10402022;Password=3fY^gK2:zb_8hZ-q";
builder.Services.AddSingleton<ITaskService>(new DapperTaskService(connectionString));

var app = builder.Build();

app.UseRewriter(new RewriteOptions().AddRedirect("tasks/(.*)", "/todos/$1"));

app.Use(async (context, next)=>{
    Console.WriteLine($"[{context.Request.Method} {context.Request.Path} {DateTime.UtcNow}]Started.");
    await next(context);
    Console.WriteLine($"[{context.Request.Method} {context.Request.Path} {DateTime.UtcNow}] Finished.");
});

app.MapGet("/todos", async (ITaskService service) => await service.GetTodos());

app.MapGet("/todos/{id}", async (int id, ITaskService service) =>
{
    var targetTodo = await service.GetTodoById(id);
    return targetTodo != null
        ? TypedResults.Ok(targetTodo)
        : Results.NotFound();
});

app.MapDelete("/todos/{id}", async (int id, ITaskService service) => {
    await service.DeleteTodoById(id);
    return TypedResults.NoContent();
});

app.MapPost("/todos", async (Todo task, ITaskService service) => 
{
    var addedTask = await service.AddTodo(task);
    return TypedResults.Created($"/todos/{addedTask.Id}", addedTask);
})
.AddEndpointFilter(async (context, next) => {
    var taskArgument = context.GetArgument<Todo>(0);
    var errors = new Dictionary<string, string[]>();

    if( taskArgument.DueDate < DateTime.UtcNow){
       errors.Add(nameof(Todo.DueDate), ["Cannot have due date in the past"]); 
    }
    if(taskArgument.IsCompleted){
        errors.Add(nameof(Todo.IsCompleted), ["Task is already completed. Cannot add completed todo."]);
    }

    if(errors.Count > 0){
        return Results.ValidationProblem(errors);
    }

    return await next(context);
});

app.Run();

public record Todo(int Id, string Name, DateTime DueDate, bool IsCompleted);

interface ITaskService {
    Task<Todo?> GetTodoById(int id);
    Task<List<Todo>> GetTodos();
    Task DeleteTodoById(int id);
    Task<Todo> AddTodo(Todo task);
}

// Rest of the code remains the same...
class DapperTaskService : ITaskService
{
    private readonly string _connectionString;

    public DapperTaskService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Todo?> GetTodoById(int id)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var query = "SELECT id, name, duedate, iscompleted FROM todos WHERE id = @Id";
        return await connection.QuerySingleOrDefaultAsync<Todo>(query, new { Id = id });
    }

    public async Task<List<Todo>> GetTodos()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var query = "SELECT id, name, duedate, iscompleted FROM todos";
        var todos = await connection.QueryAsync<Todo>(query);
        return todos.AsList();
    }

    public async Task DeleteTodoById(int id)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var query = "DELETE FROM todos WHERE id = @Id";
        await connection.ExecuteAsync(query, new { Id = id });
    }

    public async Task<Todo> AddTodo(Todo task)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        var query = @"
            INSERT INTO todos (name, duedate, iscompleted) 
            VALUES (@Name, @DueDate, @IsCompleted) 
            RETURNING id";
        var id = await connection.ExecuteScalarAsync<int>(query, task);
        return task with { Id = id };
    }
}