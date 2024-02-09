using System.Net.Http.Json;

namespace Tests;

public class SomeTests : IClassFixture<ApplicationFactory<Todos.Program>>
{
    private readonly ApplicationFactory<Todos.Program> factory;

    public SomeTests(ApplicationFactory<Todos.Program> factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task CreateTodo()
    {
        // Given
        var client = factory.CreateClient();
        var dto = new Todos.CreateTodoDto("My title", "My description");

        // When
        var response = await client.PostAsJsonAsync<Todos.CreateTodoDto>("/api/todo", dto);
        var result = await response.Content.ReadFromJsonAsync<Todos.TodoDto>();

        // Then
        response.EnsureSuccessStatusCode();
        Assert.NotNull(result);
        Assert.Equal("My title", result.Title);
        Assert.Equal("My description", result.Description);
    }

    [Fact]
    public async Task CreateTodoBadInput()
    {
        // Given
        var client = factory.CreateClient();
        var dto = new Todos.CreateTodoDto("", "My description");

        // When
        var response = await client.PostAsJsonAsync<Todos.CreateTodoDto>("/api/todo", dto);

        // Then
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }
}
