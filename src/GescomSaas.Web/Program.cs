using GescomSaas.Infrastructure;
using GescomSaas.Infrastructure.Configuration;
using GescomSaas.Infrastructure.Identity;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Api;
using Microsoft.AspNetCore.Identity;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.Configure<PlatformNotificationEmailOptions>(
    builder.Configuration.GetSection(PlatformNotificationEmailOptions.SectionName));
builder.Services.AddInfrastructure(connectionString);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddAuthorization();
builder.Services.AddAuthentication()
    .AddBearerToken(IdentityConstants.BearerScheme);
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddApiEndpoints();
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, ApplicationUserClaimsPrincipalFactory>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "LigCom API",
        Version = "v1",
        Description = "API REST SaaS de gestion commerciale inspiree de Sage Gescom 100."
    });
    options.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "Opaque",
        In = ParameterLocation.Header,
        Description = "Colle ici le bearer token obtenu via POST /api/identity/login?useCookies=false."
    });
    options.OperationFilter<AuthorizeOperationFilter>();
});
builder.Services.AddRazorPages();

var app = builder.Build();

if (args.Any(x => string.Equals(x, "--drop-database", StringComparison.OrdinalIgnoreCase)))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.EnsureDeletedAsync();
}

await app.Services.SeedApplicationAsync();
if (args.Any(x => string.Equals(x, "--seed-only", StringComparison.OrdinalIgnoreCase)))
{
    return;
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "LigCom API v1");
    options.RoutePrefix = "swagger";
    options.DisplayRequestDuration();
    options.EnablePersistAuthorization();
    options.DocumentTitle = "LigCom API";
});

app.MapStaticAssets();
app.MapGroup("/api/identity")
    .WithTags("Identity API")
    .MapIdentityApi<ApplicationUser>();
app.MapGescomApi();
app.MapRazorPages()
    .WithStaticAssets();

app.Run();
