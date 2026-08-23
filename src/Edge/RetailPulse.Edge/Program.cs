using RetailPulse.BuildingBlocks;
using RetailPulse.Edge;

var builder = WebApplication.CreateBuilder(args);
var databasePath = builder.Configuration["RetailPulse:EdgeDatabasePath"] ?? Path.Combine(AppContext.BaseDirectory, "retailpulse-edge.db");
builder.Services.AddSingleton<ILocalCheckoutPersistence>(_ => new SqliteCheckoutPersistence(databasePath));
var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
