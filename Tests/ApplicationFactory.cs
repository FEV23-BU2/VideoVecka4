using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class ApplicationFactory<T> : WebApplicationFactory<T>
    where T : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.AddDbContext<Todos.TodoDbContext>(options =>
            {
                var path = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData
                );
                options.UseSqlite($"Data source={Path.Join(path, "TodosTestDb.db")}");
            });

            services
                .AddAuthentication("TestScheme")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    "TestScheme",
                    options => { }
                );

            var provider = services.BuildServiceProvider();

            var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<Todos.TodoDbContext>();

            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            var user = new Todos.User();
            user.Email = "userid";
            user.Id = "userid";

            context.Users.Add(user);
            context.SaveChanges();
        });
    }
}

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder
    )
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "myuser"),
            new Claim(ClaimTypes.NameIdentifier, "userid"),
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");

        var result = AuthenticateResult.Success(ticket);
        return Task.FromResult(result);
    }
}
