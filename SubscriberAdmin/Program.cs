using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options => {
    options.LoginPath = "/signin";
    options.LogoutPath = "/signout";
    options.AccessDeniedPath = "/accessdenied";
});
builder.Services.AddAuthorization(o => o.AddPolicy("AdminsOnly", b => b.RequireClaim(ClaimTypes.Role, "Admin")));

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

builder.Services.AddDbContext<SubscriberContext>(options => options.UseInMemoryDatabase("subs"));

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseSession();

// LINE Login
app.MapGet("/signin", LINELoginHandler.SignIn).WithName(nameof(LINELoginHandler.SignIn)).AllowAnonymous();
app.MapGet("/signin-callback", LINELoginHandler.SigninCallback).WithName(nameof(LINELoginHandler.SigninCallback)).AllowAnonymous();
app.MapGet("/signout", LINELoginHandler.SignOut).WithName(nameof(LINELoginHandler.SignOut)).AllowAnonymous();

// LINE Notify
app.MapGet("/subscribe", LINENotifyHandler.Subscribe).WithName(nameof(LINENotifyHandler.Subscribe)).RequireAuthorization();
app.MapGet("/subscribe-callback", LINENotifyHandler.SubscribeCallback).WithName(nameof(LINENotifyHandler.SubscribeCallback)).RequireAuthorization();
app.MapGet("/unsubscribe", LINENotifyHandler.Unsubscribe).WithName(nameof(LINENotifyHandler.Unsubscribe)).RequireAuthorization();

// Normal User
app.MapGet("/profile", LINELoginHandler.Profile).WithName(nameof(LINELoginHandler.Profile)).RequireAuthorization();
app.MapGet("/my", DefaultHandler.My).WithName(nameof(DefaultHandler.My)).RequireAuthorization();

// AdminsOnly
app.MapGet("/all", DefaultHandler.AllSubscribers).WithName(nameof(DefaultHandler.AllSubscribers)).RequireAuthorization("AdminsOnly");
app.MapGet("/notifyall", LINENotifyHandler.NotifyAll).WithName(nameof(LINENotifyHandler.NotifyAll)).RequireAuthorization("AdminsOnly");

// Others
app.MapGet("/accessdenied", () => {
    return Results.Ok("Access Denied!");
}).WithName("accessdenied").AllowAnonymous();

app.MapGet("/claims", ([FromServices] IHttpContextAccessor httpContextAccessor) => {
    ClaimsIdentity claimsIdentity = httpContextAccessor.HttpContext.User.Identity as ClaimsIdentity;
    return Results.Ok(claimsIdentity.Claims.ToDictionary(c => c.Type, c => c.Value));
}).WithName("claims").RequireAuthorization();

app.Run();
