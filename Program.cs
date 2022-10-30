using EasyFortniteStats_ImageApi;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<SharedAssets>();

var app = builder.Build();

app.Services.GetRequiredService<IMemoryCache>().Set("shop_template_mutex", new Mutex());
app.Services.GetRequiredService<IMemoryCache>().Set("stats_normal_template_mutex", new Mutex());
app.Services.GetRequiredService<IMemoryCache>().Set("stats_competitive_template_mutex", new Mutex());

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();