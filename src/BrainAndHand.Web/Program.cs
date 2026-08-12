using System.Security.Claims;
using BrainAndHand.Data;
using BrainAndHand.Web.Client.Pages;
using BrainAndHand.Web.Components;
using BrainAndHand.Web.Hubs;
using BrainAndHand.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents()
    // Default only serializes name/role claims to the client — we need our custom "buid" claim too.
    .AddAuthenticationStateSerialization(options => options.SerializeAllClaims = true);

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddDbContext<BrainAndHandDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=brainandhand.db"));
builder.Services.AddScoped<UserService>();

builder.Services.AddSignalR();
builder.Services.AddSingleton<RoomRegistry>();
builder.Services.AddSingleton<BotRunner>();
builder.Services.AddHostedService<ClockWatchdog>();
builder.Services.AddScoped<BrainAndHand.Web.Client.Services.GameClient>();

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
    var db = scope.ServiceProvider.GetRequiredService<BrainAndHandDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
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
    .AddAdditionalAssemblies(typeof(BrainAndHand.Web.Client._Imports).Assembly);

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

app.Run();
