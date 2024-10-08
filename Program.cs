using AsyncKeyedLock;
using EasyFortniteStats_ImageApi;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<SharedAssets>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton(new AsyncKeyedLocker<string>(o =>
{
    o.PoolSize = 20;
    o.PoolInitialFill = 1;
}));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();