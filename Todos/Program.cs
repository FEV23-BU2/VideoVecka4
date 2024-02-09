namespace Todos;

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(
                "create_todo",
                policy =>
                {
                    policy.RequireAuthenticatedUser();
                }
            );
            options.AddPolicy(
                "role_add",
                policy =>
                {
                    policy.RequireAuthenticatedUser();
                }
            );
            options.AddPolicy(
                "remove_todo",
                policy =>
                {
                    policy.RequireAuthenticatedUser().RequireRole("remove");
                }
            );
            options.AddPolicy(
                "update_todo",
                policy =>
                {
                    policy.RequireAuthenticatedUser();
                }
            );
            options.AddPolicy(
                "get_todos",
                policy =>
                {
                    policy.RequireAuthenticatedUser();
                }
            );
        });

        builder.Services.AddDbContext<TodoDbContext>(
            options =>
                options.UseNpgsql(
                    "Host=localhost;Database=videovecka2;Username=postgres;Password=password"
                )
        );

        builder.Services.AddAuthentication().AddBearerToken(IdentityConstants.BearerScheme);
        builder
            .Services.AddIdentityCore<User>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<TodoDbContext>()
            .AddApiEndpoints();
        builder.Services.AddControllers();
        builder.Services.AddScoped<TodoService, TodoService>();
        builder.Services.AddTransient<IClaimsTransformation, MyClaimsTransformation>();

        var app = builder.Build();

        app.MapIdentityApi<User>();
        app.MapControllers();
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();

        app.Run();
    }
}

public class MyClaimsTransformation : IClaimsTransformation
{
    UserManager<User> userManager;

    public MyClaimsTransformation(UserManager<User> userManager)
    {
        this.userManager = userManager;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ClaimsIdentity claims = new ClaimsIdentity();

        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (id != null)
        {
            var user = await userManager.FindByIdAsync(id);
            if (user != null)
            {
                var userRoles = await userManager.GetRolesAsync(user);
                foreach (var userRole in userRoles)
                {
                    claims.AddClaim(new Claim(ClaimTypes.Role, userRole));
                }
            }
        }

        principal.AddIdentity(claims);
        return await Task.FromResult(principal);
    }
}

public class User : IdentityUser
{
    public List<Todo> Todos { get; set; }

    public User() { }
}

public class Todo
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public bool Completed { get; set; }
    public User User { get; set; }

    public Todo() { }

    public Todo(string title, string description, User user)
    {
        this.Title = title;
        this.Description = description;
        this.Completed = false;
        this.User = user;
    }
}

public class TodoDto
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public bool Completed { get; set; }

    public TodoDto() { }

    public TodoDto(Todo todo)
    {
        this.Id = todo.Id;
        this.Title = todo.Title;
        this.Description = todo.Description;
        this.Completed = todo.Completed;
    }
}

public class TodoDbContext : IdentityDbContext<User>
{
    public DbSet<Todo> Todos { get; set; }

    public TodoDbContext(DbContextOptions<TodoDbContext> options)
        : base(options) { }
}

public class CreateTodoDto
{
    public string Title { get; set; }
    public string Description { get; set; }

    public CreateTodoDto(string title, string description)
    {
        this.Title = title;
        this.Description = description;
    }
}

[ApiController]
[Route("api")]
public class TodoController : ControllerBase
{
    private TodoService todoService;
    private RoleManager<IdentityRole> roleManager;
    private UserManager<User> userManager;
    private TodoDbContext context;

    public TodoController(
        TodoService todoService,
        RoleManager<IdentityRole> roleManager,
        UserManager<User> userManager,
        TodoDbContext context
    )
    {
        this.todoService = todoService;
        this.roleManager = roleManager;
        this.userManager = userManager;
        this.context = context;
    }

    [HttpPost("role/{role}")]
    public async Task<string> CreateRole(string role)
    {
        await roleManager.CreateAsync(new IdentityRole(role));
        return "Added role!";
    }

    [HttpPost("role-add/{role}")]
    [Authorize("role_add")]
    public async Task<string> AddUserRole(string role)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        User? user = context.Users.Find(id);
        if (user == null)
        {
            return "No such user";
        }

        await userManager.AddToRoleAsync(user, role);
        return "Added user to role!";
    }

    [HttpPost("todo")]
    [Authorize("create_todo")]
    public IActionResult CreateTodo([FromBody] CreateTodoDto dto)
    {
        try
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Todo todo = todoService.CreateTodo(dto.Title, dto.Description, id);

            TodoDto output = new TodoDto(todo);
            return Ok(output);
        }
        catch (ArgumentException)
        {
            return BadRequest();
        }
    }

    [HttpDelete("todo/{id}")]
    [Authorize("remove_todo")]
    public IActionResult RemoveTodo(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Todo? todo = todoService.RemoveTodo(id, userId);
        if (todo == null)
        {
            return NotFound();
        }

        TodoDto output = new TodoDto(todo);
        return Ok(output);
    }

    [HttpPut("todo/{id}")]
    [Authorize("update_todo")]
    public IActionResult UpdateTodo(int id, [FromQuery] bool completed)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return NotFound();
        }

        Todo? todo = todoService.UpdateTodo(id, completed, userId);
        if (todo == null)
        {
            return NotFound();
        }

        TodoDto output = new TodoDto(todo);
        return Ok(output);
    }

    [HttpGet("todos")]
    [Authorize("get_todos")]
    public List<TodoDto> GetAllTodos()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return todoService.GetAllTodos(userId).Select(todo => new TodoDto(todo)).ToList();
    }
}

public class TodoService
{
    private TodoDbContext context;

    public TodoService(TodoDbContext context)
    {
        this.context = context;
    }

    public Todo CreateTodo(string title, string description, string id)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title must not be null or empty");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description must not be null or empty");
        }

        User? user = context.Users.Find(id);
        if (user == null)
        {
            throw new ArgumentException("No such user.");
        }

        Todo todo = new Todo(title, description, user);
        context.Todos.Add(todo);
        user.Todos.Add(todo);
        context.SaveChanges();
        return todo;
    }

    public Todo? RemoveTodo(int id, string userId)
    {
        User? user = context.Users.Find(userId);
        if (user == null)
        {
            throw new ArgumentException("No such user.");
        }

        List<Todo> todos = context
            .Todos.Where(todo => todo.User.Id == user.Id && todo.Id == id)
            .ToList();
        if (todos.Count == 0)
        {
            return null;
        }

        Todo todo = todos[0];

        context.Todos.Remove(todo);
        context.SaveChanges();

        return todo;
    }

    public Todo? UpdateTodo(int id, bool completed, string userId)
    {
        User? user = context.Users.Find(userId);
        if (user == null)
        {
            throw new ArgumentException("No such user.");
        }

        List<Todo> todos = context
            .Todos.Where(todo => todo.User.Id == user.Id && todo.Id == id)
            .ToList();
        if (todos.Count == 0)
        {
            return null;
        }

        Todo todo = todos[0];

        todo.Completed = completed;
        context.SaveChanges();

        return todo;
    }

    public List<Todo> GetAllTodos(string userId)
    {
        User? user = context.Users.Find(userId);
        if (user == null)
        {
            return new List<Todo>();
        }

        return context.Todos.Where(todo => todo.User.Id == user.Id).ToList();
    }
}
