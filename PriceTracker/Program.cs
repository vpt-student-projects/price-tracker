using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PriceTracker.Data;
using PriceTracker.Middleware;
using PriceTracker.Models;
using PriceTracker.Services;
using PriceTracker.Workers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.WriteIndented = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PriceTracker API",
        Version = "v1",
        Description = "API для отслеживания цен на товары"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient("scraper")
    .ConfigureHttpClient(c =>
    {
        c.Timeout = TimeSpan.FromSeconds(30);
    });

builder.Services.AddHostedService<PriceWorker>();
builder.Services.AddTransient<AdvancedPriceParser>();
builder.Services.AddScoped<IAuthService, AuthService>();

var app = builder.Build();

app.UseCors("AllowAll");

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("🔄 Создание базы данных...");

        await dbContext.Database.EnsureCreatedAsync();

        logger.LogInformation("✅ База данных успешно создана");

        bool adminExists = false;
        try
        {
            adminExists = await dbContext.Users.AnyAsync(u => u.Username == "admin");
        }
        catch
        {
            adminExists = false;
        }

        if (!adminExists)
        {
            using var hmac = new HMACSHA512();
            var salt = hmac.Key;
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes("admin123"));

            var admin = new User
            {
                Username = "admin",
                Email = "admin@pricetracker.com",
                PasswordHash = Convert.ToBase64String(hash),
                Salt = Convert.ToBase64String(salt),
                Role = "Admin",
                CreatedAt = DateTime.Now, 
                IsActive = true
            };

            await dbContext.Users.AddAsync(admin);
            await dbContext.SaveChangesAsync();

            logger.LogInformation("✅ Создан администратор: admin/admin123");
            logger.LogInformation($"📅 Время создания: {admin.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        }
        else
        {
            logger.LogInformation("ℹ️ Администратор уже существует");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Ошибка при инициализации базы данных");
    }
}

app.UseMiddleware<JwtMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PriceTracker API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseAuthorization();
app.MapControllers();

app.Run();