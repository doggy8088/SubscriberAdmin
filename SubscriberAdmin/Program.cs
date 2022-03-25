using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<SubscriberContext>(options => options.UseInMemoryDatabase("subs"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/subs", async (SubscriberContext db) => await db.Subscribers.ToListAsync())
    .WithName("GetSubscribers");

app.MapGet("/subs/{id}", async (SubscriberContext db, int id) =>
{
    var sub = await db.Subscribers.FindAsync(id);
    if (sub is null) return Results.NotFound();
    return Results.Ok(sub);
}).WithName("GetSubscriberById");

app.MapPost("/subs", async (SubscriberContext db, Subscriber sub) =>
{
    await db.Subscribers.AddAsync(sub);
    await db.SaveChangesAsync();
    return Results.Created($"/subs/{sub.Id}", sub);
}).WithName("AddSubscriber");

app.MapPut("/subs/{id}", async (SubscriberContext db, Subscriber updatesub, int id) =>
{
    var sub = await db.Subscribers.FindAsync(id);
    if (sub is null) return Results.NotFound();
    sub.Username = updatesub.Username;
    sub.AccessToken = updatesub.AccessToken;
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapDelete("/subs/{id}", async (SubscriberContext db, int id) =>
{
    var sub = await db.Subscribers.FindAsync(id);
    if (sub is null)
    {
        return Results.NotFound();
    }
    db.Subscribers.Remove(sub);
    await db.SaveChangesAsync();
    return Results.Ok();
}).WithName("DeleteSubscriber");

app.Run();
