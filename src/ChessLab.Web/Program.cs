using System.Security.Claims;
using ChessLab.Data;
using ChessLab.Web.Client.Pages;
using ChessLab.Web.Components;
using ChessLab.Web.Hubs;
using ChessLab.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var app = Program.CreateApp(args);
app.Run();

public partial class Program
{
    public static WebApplication CreateApp(
        string[] args,
        Action<WebApplicationBuilder>? configureForTests = null,
        string? environment = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            EnvironmentName = environment,
            ApplicationName = typeof(Program).Assembly.GetName().Name,
        });
        configureForTests?.Invoke(builder);

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents()
            .AddAuthenticationStateSerialization(options => options.SerializeAllClaims = true);

        builder.Services.AddCascadingAuthenticationState();

        var connectionString = builder.Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Missing configuration: ConnectionStrings:Default");
        var useSqlite = connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase);
        builder.Services.AddDbContext<ChessLabDbContext>(options =>
        {
            if (useSqlite)
                options.UseSqlite(connectionString);
            else
                options.UseNpgsql(connectionString);
        });
        builder.Services.AddScoped<UserService>();
        builder.Services.AddScoped<GameHistoryService>();

        builder.Services.AddDataProtection().PersistKeysToDbContext<ChessLabDbContext>();

        builder.Services.AddSignalR().AddMessagePackProtocol();
        builder.Services.AddSingleton<RoomRegistry>();
        builder.Services.AddSingleton<BotRunner>();
        builder.Services.AddSingleton<GameArchive>();
        builder.Services.AddHostedService<ClockWatchdog>();
        builder.Services.AddScoped<ChessLab.Web.Client.Services.GameClient>();
        builder.Services.AddScoped<ChessLab.Web.Client.Services.CurrentGameContext>();

        builder.Services.AddAuthorization();
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            })
            .AddGoogle(options =>
            {
                options.ClientId = builder.Configuration["Authentication:Google:ClientId"]
                    ?? throw new InvalidOperationException("Missing configuration: Authentication:Google:ClientId");
                options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]
                    ?? throw new InvalidOperationException("Missing configuration: Authentication:Google:ClientSecret");
                options.CallbackPath = "/signin-google";
                options.ClaimActions.MapJsonKey("urn:google:picture", "picture");

                options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

                options.Events.OnCreatingTicket = async context =>
                {
                    var principal = context.Principal!;
                    var subjectId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
                    var email = principal.FindFirstValue(ClaimTypes.Email) ?? "";
                    var name = principal.FindFirstValue(ClaimTypes.Name) ?? email;
                    var avatarUrl = principal.FindFirstValue("urn:google:picture");

                    var userService = context.HttpContext.RequestServices.GetRequiredService<UserService>();
                    var user = await userService.UpsertFromGoogleLoginAsync(subjectId, email, name, avatarUrl);

                    ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim("buid", user.Id.ToString()));
                };
            });

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ChessLabDbContext>();
            if (useSqlite)
                db.Database.EnsureCreated();
            else
                db.Database.Migrate();
        }

        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            KnownIPNetworks = { },
            KnownProxies = { },
        });

        if (app.Environment.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
        }
        else
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            app.UseHsts();
        }
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseHttpsRedirection();

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(ChessLab.Web.Client._Imports).Assembly);

        app.MapHub<GameHub>("/hubs/game");

        app.MapGet("/Account/Login", (string? returnUrl) =>
            Results.Challenge(
                new Microsoft.AspNetCore.Authentication.AuthenticationProperties { RedirectUri = returnUrl ?? "/" },
                [GoogleDefaults.AuthenticationScheme]));

        app.MapPost("/Account/Logout", async (HttpContext http, string? returnUrl) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.LocalRedirect(returnUrl ?? "/");
        });

        if (app.Configuration.GetValue<bool>("E2E_TEST_AUTH"))
        {
            app.MapGet("/TestAuth/Login", async (HttpContext http, string userId, string name) =>
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, $"test-{userId}"),
                    new Claim(ClaimTypes.Name, name),
                    new Claim("buid", userId),
                };
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
                return Results.Redirect("/");
            });
        }

        return app;
    }
}
