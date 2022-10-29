using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddMemoryCache();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Setup the cache
app.Services.GetRequiredService<IMemoryCache>().Set("shop_template_mutex", new Mutex());
app.Services.GetRequiredService<IMemoryCache>().Set("stats_normal_template_mutex", new Mutex());
app.Services.GetRequiredService<IMemoryCache>().Set("stats_competitive_template_mutex", new Mutex());

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();