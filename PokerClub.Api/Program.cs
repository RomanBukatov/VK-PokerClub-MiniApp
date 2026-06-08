using Microsoft.EntityFrameworkCore;
using PokerClub.Domain.Interfaces;
using PokerClub.Infrastructure.Data;
using PokerClub.Infrastructure.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Подключаем PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Регистрируем сервисы бизнес-логики
builder.Services.AddScoped<ITournamentService, TournamentService>();
builder.Services.AddScoped<IRatingService, RatingService>();

builder.Services.AddControllers();

// Включаем встроенную генерацию OpenAPI
builder.Services.AddOpenApi(); 

var app = builder.Build();

// ==========================================
// АВТОМАТИЧЕСКИЕ МИГРАЦИИ ПРИ СТАРТЕ
// ==========================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        // Накатываем все недостающие миграции
        await context.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Бро, база отвалилась при накатывании миграций!");
    }
}


// Врубаем Scalar в режиме разработки
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // Генерит swagger.json
    app.MapScalarApiReference(options => 
    {
        options.Title = "Poker Club API";
        options.Theme = ScalarTheme.DeepSpace; // Темная тема, под покер — кайф!
    });
}

app.UseAuthorization();
app.MapControllers();

app.Run();