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
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var app = Program.CreateApp(args);
app.Run();

public partial class Program
{
    /// <summary>
    /// Builds and configures the app without starting it. Factored out of the top-level
    /// statements so the E2E test project can boot the exact same app — real Kestrel listener,
    /// real middleware pipeline, real DB migration — against throwaway test configuration,
    /// instead of relying on <c>WebApplicationFactory</c>'s TestServer (which doesn't speak
    /// WebSockets and proved unreliable at resolving a real bound port).
    /// </summary>
    public static WebApplication CreateApp(
        string[] args,
        Action<WebApplicationBuilder>? configureForTests = null,
        string? environment = null)
    {
        // EnvironmentName must go through WebApplicationOptions — WebApplicationBuilder locks
        // the environment as soon as it's constructed, so callers can't change it afterwards
        // via builder.WebHost.UseEnvironment(...). ApplicationName is pinned explicitly too:
        // it otherwise defaults to the entry assembly, which under the E2E test host is
        // "testhost" rather than "ChessLab.Web" — MapStaticAssets() uses ApplicationName
        // to find "{ApplicationName}.staticwebassets.endpoints.json" and would fail to locate it.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            EnvironmentName = environment,
            ApplicationName = typeof(Program).Assembly.GetName().Name,
        });
        configureForTests?.Invoke(builder);

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents()
            // Default only serializes name/role claims to the client — we need our custom "buid" claim too.
            .AddAuthenticationStateSerialization(options => options.SerializeAllClaims = true);

        builder.Services.AddCascadingAuthenticationState();

        var connectionString = builder.Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Missing configuration: ConnectionStrings:Default");
        // SQLite is only ever used for the E2E test fixture's throwaway per-test database
        // (WebAppFixture) — production and local dev always run against Postgres.
        var useSqlite = connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase);
        builder.Services.AddDbContext<ChessLabDbContext>(options =>
        {
            if (useSqlite)
                options.UseSqlite(connectionString);
            else
                options.UseNpgsql(connectionString);
        });
        builder.Services.AddScoped<UserService>();

        builder.Services.AddSignalR().AddMessagePackProtocol();
        builder.Services.AddSingleton<RoomRegistry>();
        builder.Services.AddSingleton<BotRunner>();
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
                // Same reasoning as the correlation cookie below: don't force Secure so the sign-in
                // cookie also works when the app is served over plain http (e.g. the Docker container).
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

                // Google's redirect back to us is a top-level GET, which SameSite=Lax still allows,
                // so the correlation cookie doesn't need Secure — lets this work over plain http
                // (local dev, or the Docker container which doesn't terminate TLS itself).
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
            // The SQLite test path has no migrations of its own (see useSqlite above) — the
            // schema is created directly from the current model instead.
            if (useSqlite)
                db.Database.EnsureCreated();
            else
                db.Database.Migrate();
        }

        // Configure the HTTP request pipeline.
        // Must run before anything that inspects Request.Scheme (HTTPS redirection, HSTS, the
        // Google OAuth handler building its redirect_uri) — Render (and similar PaaS) terminate
        // TLS at their edge and forward plain http to the container, so without this the app
        // thinks every request is http and Google rejects the resulting redirect_uri as a mismatch.
        // KnownNetworks/KnownProxies are cleared because the edge proxy's IP isn't fixed/known.
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
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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

        // Only ever active when explicitly opted into (E2E_TEST_AUTH=true) — lets automated browser
        // tests sign in with a real auth cookie without going through Google. Never set in production.
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
