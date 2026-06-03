using Microsoft.EntityFrameworkCore;
using PokerClub.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Подключаем PostgreSQL из нашего конфига
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseAuthorization();
app.MapControllers();

app.Run();